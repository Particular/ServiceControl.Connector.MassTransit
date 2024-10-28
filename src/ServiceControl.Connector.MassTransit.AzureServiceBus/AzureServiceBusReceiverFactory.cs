using Azure.Messaging.ServiceBus;
using NServiceBus.Transport;

class AzureServiceBusReceiverFactory(SubQueue subQueue) : ReceiverFactory
{
    static readonly string SubQueueKey = typeof(SubQueue).FullName!;
    static readonly IReadOnlyDictionary<string, string> DeadLetterQueue = new Dictionary<string, string>(1) { { SubQueueKey, SubQueue.DeadLetter.ToString() } };

    public override ReceiveSettings Create(string inputQueue, string errorQueue)
    {
        if (subQueue == SubQueue.TransferDeadLetter)
        {
            throw new NotSupportedException(nameof(SubQueue.TransferDeadLetter));
        }
        var queueProperties = subQueue == SubQueue.DeadLetter
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