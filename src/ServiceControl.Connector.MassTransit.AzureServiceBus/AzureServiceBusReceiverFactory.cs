using Azure.Messaging.ServiceBus;
using NServiceBus.Transport;

class AzureServiceBusReceiverFactory(ReceiveMode receiveMode) : ReceiverFactory
{
    static readonly string SubQueueKey = typeof(SubQueue).FullName!;
    static readonly IReadOnlyDictionary<string, string> DeadLetterQueue = new Dictionary<string, string>(1) { { SubQueueKey, ReceiveMode.DeadLetterQueue.ToString() } };

    public override ReceiveSettings Create(string inputQueue, string errorQueue)
    {
        var queueProperties = receiveMode == ReceiveMode.DeadLetterQueue
            ? DeadLetterQueue
            : null;

        return new ReceiveSettings(
            id: inputQueue,
            receiveAddress: new QueueAddress(inputQueue, properties: queueProperties),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorQueue
        );
    }
}