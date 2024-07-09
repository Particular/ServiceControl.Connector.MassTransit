using NServiceBus.Transport;

public class ReceiverFactory
{
    public virtual ReceiveSettings Create(string errorInputQueue)
    {
        return new ReceiveSettings(
            id: errorInputQueue,
            receiveAddress: new QueueAddress(errorInputQueue),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorInputQueue + ".error"
        );
    }
}