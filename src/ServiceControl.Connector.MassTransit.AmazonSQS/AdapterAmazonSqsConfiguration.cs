using System.Text;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAmazonSqsConfiguration
{
    public static void UsingAmazonSqs(this IServiceCollection services, Action<SqsTransport>? transportConfig = null)
    {
        services.AddSingleton<IQueueInformationProvider>(new AmazonSqsHelper(string.Empty));
        services.AddSingleton(sp => new TransportDefinitionFactory(async (HostSettings hostSettings, ReceiveSettings[] receivers,
            string[] sendingAddresses, CancellationToken cancellationToken) =>
        {
            var transport = new SqsTransport(enableDelayedDelivery: false) { DoNotWrapOutgoingMessages = true };
            transportConfig?.Invoke(transport);

            var infrastructure =
                await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);

            var configuration = sp.GetRequiredService<Configuration>();

            return new Wrapper(infrastructure, new CustomSQSDispatcher(infrastructure.Dispatcher, configuration.ErrorQueue));
        }));
        services.AddSingleton<MassTransitFailureAdapter, AmazonSqsMassTransitFailureAdapter>();
    }

    class Wrapper : TransportInfrastructure
    {
        readonly TransportInfrastructure infrastructure;

        public Wrapper(TransportInfrastructure infrastructure, IMessageDispatcher customDispatcher)
        {
            this.infrastructure = infrastructure;

            Receivers = infrastructure.Receivers;
            Dispatcher = customDispatcher;
        }

        public override Task Shutdown(CancellationToken cancellationToken = new CancellationToken()) => infrastructure.Shutdown(cancellationToken);

        public override string ToTransportAddress(QueueAddress address) => infrastructure.ToTransportAddress(address);
    }
}

public class CustomSQSDispatcher : IMessageDispatcher
{
    readonly IMessageDispatcher defaultDispatcher;
    readonly string errorQueue;

    public CustomSQSDispatcher(IMessageDispatcher defaultDispatcher, string errorQueue)
    {
        this.defaultDispatcher = defaultDispatcher;
        this.errorQueue = errorQueue;
    }

    public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction,
        CancellationToken cancellationToken = new CancellationToken())
    {
        if (outgoingMessages.UnicastTransportOperations.Count == 1 &&
            outgoingMessages.UnicastTransportOperations[0].Destination == errorQueue)
        {
            await defaultDispatcher.Dispatch(outgoingMessages, transaction, cancellationToken);
            return;
        }

        var client = new AmazonSQSClient();


        var message = outgoingMessages.UnicastTransportOperations[0].Message;
        var massTransitReturnQueueName = message.Headers["MT-Fault-InputAddress"];
        var queueName = massTransitReturnQueueName.Substring(massTransitReturnQueueName.LastIndexOf('/') + 1);
        var getQueueUrlResponse = await client.GetQueueUrlAsync(queueName, cancellationToken);

        //TODO: ensure we extract the proper queue url from the headers
        var sqsMessage = new SendMessageRequest(getQueueUrlResponse.QueueUrl, Encoding.UTF8.GetString(message.Body.Span));

        var attributes = new Dictionary<string, MessageAttributeValue>();

        //TODO: make sure we don't exceed 10 headers limit. If so remove, SC related headers
        foreach (KeyValuePair<string, string> header in message.Headers)
        {
            attributes.Add(header.Key, new MessageAttributeValue { StringValue = header.Value, DataType = "String"});
        }

        sqsMessage.MessageAttributes = attributes;

        await client.SendMessageAsync(sqsMessage, cancellationToken);
    }
}
