using NServiceBus.Transport;

class AzureServiceBusReceiverFactory(ReceiveMode receiveMode) : ReceiverFactory
{
    static readonly IReadOnlyDictionary<string, string> NormalQueue = new Dictionary<string, string>(1) { { nameof(ReceiveMode), ReceiveMode.Queue.ToString() } };
    static readonly IReadOnlyDictionary<string, string> DeadLetterQueue = new Dictionary<string, string>(1) { { nameof(ReceiveMode), ReceiveMode.DeadLetterQueue.ToString() } };

    public override ReceiveSettings Create(string inputQueue, string errorQueue)
    {
        var queueProperties = receiveMode == ReceiveMode.DeadLetterQueue
            ? DeadLetterQueue
            : NormalQueue;

        return new ReceiveSettings(
            id: inputQueue,
            receiveAddress: new QueueAddress(inputQueue, properties: queueProperties),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorQueue
        );
    }
}