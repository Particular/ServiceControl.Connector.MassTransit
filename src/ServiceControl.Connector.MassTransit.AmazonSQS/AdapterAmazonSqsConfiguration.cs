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
        services.AddSingleton(new TransportDefinitionFactory(async (HostSettings hostSettings, ReceiveSettings[] receivers,
            string[] sendingAddresses, CancellationToken cancellationToken) =>
        {
            var transport = new SqsTransport(enableDelayedDelivery: false) { DoNotWrapOutgoingMessages = true };
            transportConfig?.Invoke(transport);

            var infrastructure =
                await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);

            return new Wrapper(infrastructure, new CustomSQSDispatcher());
        }));
        services.AddSingleton<MassTransitFailureAdapter, AmazonSqsMassTransitFailureAdapter>();
    }

    class Wrapper : TransportInfrastructure
    {
        readonly TransportInfrastructure infrastructure;

        public Wrapper(TransportInfrastructure infrastructure, IMessageDispatcher customDispatcher)
        {
            this.infrastructure = infrastructure;

            Dispatcher = customDispatcher;
        }

        public override Task Shutdown(CancellationToken cancellationToken = new CancellationToken()) => infrastructure.Shutdown(cancellationToken);

        public override string ToTransportAddress(QueueAddress address) => infrastructure.ToTransportAddress(address);
    }
}

public class CustomSQSDispatcher : IMessageDispatcher
{
    public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var client = new AmazonSQSClient();
        var message = outgoingMessages.UnicastTransportOperations[0].Message;
        var queueUrl = message.Headers["MT-Fault-InputAddress"];

        //TODO: ensure we extract the proper queue url from the headers
        var sqsMessage = new SendMessageRequest(queueUrl, Encoding.UTF8.GetString(message.Body.Span));

        var attributes = new Dictionary<string, MessageAttributeValue>();

        //TODO: make sure we don't exceed 10 headers limit. If so remove, SC related headers
        foreach (KeyValuePair<string, string> header in message.Headers)
        {
            attributes.Add(header.Key, new MessageAttributeValue {StringValue = header.Value});
        }

        sqsMessage.MessageAttributes = attributes;

        await client.SendMessageAsync(sqsMessage, cancellationToken);
    }
}
