using NServiceBus.Transport;

class Wrapper : TransportInfrastructure
{
    readonly TransportInfrastructure infrastructure;

    public Wrapper(TransportInfrastructure infrastructure, IMessageDispatcher customDispatcher)
    {
        this.infrastructure = infrastructure;

        Receivers = infrastructure.Receivers;
        Dispatcher = customDispatcher;
    }

    public override Task Shutdown(CancellationToken cancellationToken = new CancellationToken()) => infrastructure.Shutdown(cancellationToken);

    public override string ToTransportAddress(QueueAddress address) => infrastructure.ToTransportAddress(address);
}