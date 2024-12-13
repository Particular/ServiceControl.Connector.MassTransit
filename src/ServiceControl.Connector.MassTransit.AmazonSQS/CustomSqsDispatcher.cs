using System.Collections.Concurrent;
using System.Text;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.Transport;

sealed class CustomSqsDispatcher(SqsTransport transport, IAmazonSQS client, IMessageDispatcher defaultDispatcher, string errorQueue) : IMessageDispatcher
{
    readonly SqsTransport transport = transport;
    readonly ConcurrentDictionary<string, Task<string>> queueUrlCache = new();

    public async Task Dispatch(
        TransportOperations outgoingMessages,
        TransportTransaction transaction,
        CancellationToken cancellationToken = new()
    )
    {
        var isForwardMessage = outgoingMessages.UnicastTransportOperations.Count == 1 &&
                               outgoingMessages.UnicastTransportOperations[0].Destination == errorQueue;

        if (isForwardMessage)
        {
            await defaultDispatcher.Dispatch(outgoingMessages, transaction, cancellationToken);
            return;
        }

        var operation = outgoingMessages.UnicastTransportOperations[0];
        var message = operation.Message;
        var massTransitReturnQueueName = operation.Destination;

        var queueUrl = await queueUrlCache.GetOrAdd(massTransitReturnQueueName, async (k) =>
        {
            var queueName = massTransitReturnQueueName.Substring(massTransitReturnQueueName.LastIndexOf('/') + 1);
            if (!queueName.StartsWith(transport.QueueNamePrefix))
            {
                queueName = transport.QueueNameGenerator(queueName, transport.QueueNamePrefix);
            }
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