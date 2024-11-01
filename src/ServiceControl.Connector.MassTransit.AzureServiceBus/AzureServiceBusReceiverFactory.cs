using Azure.Messaging.ServiceBus;
using NServiceBus.Transport;

class AzureServiceBusReceiverFactory(SubQueue subQueue) : ReceiverFactory
{
    public override ReceiveSettings Create(string inputQueue, string errorQueue)
    {
        if (subQueue == SubQueue.TransferDeadLetter)
        {
            throw new NotSupportedException(nameof(SubQueue.TransferDeadLetter));
        }

        var qualifier = subQueue == SubQueue.DeadLetter
            ? QueueAddressQualifier.DeadLetterQueue
            : null;

        return new ReceiveSettings(
            id: inputQueue,
            receiveAddress: new QueueAddress(inputQueue, qualifier: qualifier),
            usePublishSubscribe: false,
            purgeOnStartup: false,
            errorQueue: errorQueue
        );
    }
}