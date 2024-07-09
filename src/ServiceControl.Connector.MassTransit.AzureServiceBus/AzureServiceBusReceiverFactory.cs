using Microsoft.Extensions.Configuration;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;

class AzureServiceBusReceiverFactory(IConfiguration configuration) : ReceiverFactory
{
    enum ReceiveMode
    {
        Queue,
        DeadLetterQueue
    }

    readonly ReceiveMode mode = configuration.GetValue<ReceiveMode?>("ReceiveMode") ?? ReceiveMode.Queue;

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
            DeadLetterQueue = mode == ReceiveMode.DeadLetterQueue
        };
    }
}