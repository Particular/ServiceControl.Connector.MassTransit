using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;

class AzureServiceBusReceiverFactory(ReceiveMode receiveMode) : ReceiverFactory
{
    public override ReceiveSettings Create(string inputQueue, string errorQueue)
    {
        return new AzureServiceBusReceiveSettings(
            id: inputQueue,
            receiveAddress: new QueueAddress(inputQueue),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorQueue
        )
        {
            DeadLetterQueue = receiveMode == ReceiveMode.DeadLetterQueue
        };
    }
}