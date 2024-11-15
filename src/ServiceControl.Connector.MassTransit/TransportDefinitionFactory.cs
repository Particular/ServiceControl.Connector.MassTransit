using NServiceBus.Transport;

public class TransportDefinitionFactory
{
    readonly Func<HostSettings, ReceiveSettings[], string[], CancellationToken, Task<TransportInfrastructure>> factoryMethod;

    public TransportDefinitionFactory(Func<HostSettings, ReceiveSettings[], string[], CancellationToken, Task<TransportInfrastructure>> factoryMethod)
    {
        this.factoryMethod = factoryMethod;
    }

    public async Task<TransportInfrastructure> CreateTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receivers,
        string[] sendingAddresses, CancellationToken cancellationToken)
    {
        return await factoryMethod(hostSettings, receivers, sendingAddresses, cancellationToken);
    }
}