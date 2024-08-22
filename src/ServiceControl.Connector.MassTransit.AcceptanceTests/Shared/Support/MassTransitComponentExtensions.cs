using System;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

public static class MassTransitComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> WithMassTransit<TContext>(
        this IScenarioWithEndpointBehavior<TContext> scenario,
        string name,
        Action<IBusRegistrationConfigurator> busConfig,
        Action<HostBuilderContext, IServiceCollection> hostConfig = null)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new MassTransitComponent<TContext>(name, busConfig, hostConfig ?? ((_, _) =>
        {
        })));
    }
}
