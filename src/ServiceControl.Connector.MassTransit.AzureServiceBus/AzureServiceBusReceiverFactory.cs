using Microsoft.Extensions.Configuration;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;

class AzureServiceBusReceiverFactory(ReceiveMode receiveMode) : ReceiverFactory
{
    public override ReceiveSettings Create(string errorInputQueue)
    {
        return new AzureServiceBusReceiveSettings(
            id: errorInputQueue,
            receiveAddress: new QueueAddress(errorInputQueue),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorInputQueue + ".error"
        )
        {
            DeadLetterQueue = receiveMode == ReceiveMode.DeadLetterQueue
        };
    }
}