using NServiceBus.Transport;

public class ReceiverFactory
{
    public virtual ReceiveSettings Create(string inputQueue, string errorQueue)
    {
        return new ReceiveSettings(
            id: inputQueue,
            receiveAddress: new QueueAddress(inputQueue),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorQueue
        );
    }
}