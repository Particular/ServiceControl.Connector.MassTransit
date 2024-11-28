using NServiceBus.Transport;

sealed class CustomSqsTransportInfrastructure : TransportInfrastructure
{
    readonly TransportInfrastructure infrastructure;

    public CustomSqsTransportInfrastructure(TransportInfrastructure infrastructure, IMessageDispatcher customDispatcher)
    {
        this.infrastructure = infrastructure;

        Receivers = infrastructure.Receivers.ToDictionary(
            kvp => kvp.Key,
            kvp => new CustomSqsReceiver(kvp.Value) as IMessageReceiver
            ).AsReadOnly();

        Dispatcher = customDispatcher;
    }

    public override Task Shutdown(CancellationToken cancellationToken = new CancellationToken()) => infrastructure.Shutdown(cancellationToken);

    public override string ToTransportAddress(QueueAddress address) => infrastructure.ToTransportAddress(address);
}