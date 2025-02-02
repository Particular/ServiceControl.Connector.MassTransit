using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

public static class ConnectorComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> WithConnector<TContext>(
        this IScenarioWithEndpointBehavior<TContext> scenario,
        string name,
        string errorQueue,
        string returnQueue,
        string[] queueNamesToMonitor,
        string? serviceControlQueue = null)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new ConnectorComponent<TContext>(name, errorQueue, returnQueue, queueNamesToMonitor, serviceControlQueue));
    }
}