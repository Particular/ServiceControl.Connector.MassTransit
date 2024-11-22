using System.Collections.Concurrent;
using System.Text;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.Transport;

public class CustomSQSDispatcher : IMessageDispatcher
{
    readonly IAmazonSQS client;
    readonly IMessageDispatcher defaultDispatcher;
    readonly string errorQueue;
    readonly ConcurrentDictionary<string, Task<string>> queueUrlCache = new();

    public CustomSQSDispatcher(IAmazonSQS client, IMessageDispatcher defaultDispatcher, string errorQueue)
    {
        this.client = client;
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

        var message = outgoingMessages.UnicastTransportOperations[0].Message;
        var massTransitReturnQueueName = message.Headers["MT-Fault-InputAddress"];

        var queueUrl = await queueUrlCache.GetOrAdd(massTransitReturnQueueName, async (k) =>
        {
            var queueName = massTransitReturnQueueName.Substring(massTransitReturnQueueName.LastIndexOf('/') + 1);
            var getQueueUrlResponse = await client.GetQueueUrlAsync(queueName, cancellationToken);
            return getQueueUrlResponse.QueueUrl;
        });

        var sqsMessage = new SendMessageRequest(queueUrl, Encoding.UTF8.GetString(message.Body.Span));

        var attributes = new Dictionary<string, MessageAttributeValue>();

        //TODO: make sure we don't exceed 10 headers limit. If so remove, SC related headers
        foreach (KeyValuePair<string, string> header in message.Headers)
        {
            attributes.Add(header.Key, new MessageAttributeValue { StringValue = header.Value, DataType = "String" });
        }

        sqsMessage.MessageAttributes = attributes;

        await client.SendMessageAsync(sqsMessage, cancellationToken);
    }
}