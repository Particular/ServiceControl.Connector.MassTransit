using NServiceBus.Transport;

public class TransportDefinitionFactory
{
    readonly Func<TransportDefinition> factoryMethod;

    public TransportDefinitionFactory(Func<TransportDefinition> factoryMethod)
    {
        this.factoryMethod = factoryMethod;
    }

    public TransportDefinition CreateTransportDefinition()
    {
        return factoryMethod();
    }
}