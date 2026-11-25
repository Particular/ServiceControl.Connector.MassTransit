using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit;

/// <summary>
/// Creates a new transport instance. NServiceBus requires each hosted endpoint to use its own transport instance.
/// </summary>
public sealed class TransportDefinitionFactory(Func<TransportDefinition> create)
{
    public TransportDefinition Create() => create();
}

public static class ServiceControlEndpoints
{
    public const string HeartbeatEndpointName = "Heartbeat";
    public const string CustomChecksEndpointName = "CustomCheckReporter";

    /// <summary>
    /// Registers the transport used by the connector. Call it from the transport-specific configuration methods.
    /// </summary>
    public static void AddTransportDefinition(this IServiceCollection services, Func<TransportDefinition> create)
    {
        var factory = new TransportDefinitionFactory(create);
        services.AddSingleton(factory);
        services.AddTransient(_ => factory.Create());
    }

    /// <summary>
    /// Registers the send-only endpoints that report heartbeats and custom checks to ServiceControl, and the
    /// heartbeat background service. Call it after the transport has been registered.
    /// </summary>
    public static void AddServiceControlEndpoints(this IServiceCollection services, string serviceControlQueue)
    {
        var transportFactory = services
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<TransportDefinitionFactory>()
            .LastOrDefault()
            ?? throw new InvalidOperationException($"Register a transport with {nameof(AddTransportDefinition)} before calling {nameof(AddServiceControlEndpoints)}.");

        var heartbeat = CreateSendOnlyEndpoint(HeartbeatEndpointName);
        heartbeat.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
        heartbeat.UseTransport(transportFactory.Create())
            .RouteToEndpoint(typeof(MassTransitConnectorHeartbeat), serviceControlQueue);
        services.AddNServiceBusEndpoint(heartbeat, HeartbeatEndpointName);

        var customChecks = CreateSendOnlyEndpoint(CustomChecksEndpointName);
        customChecks.UseTransport(transportFactory.Create());
        customChecks.ReportCustomChecksTo(serviceControlQueue);
        customChecks.AddCustomCheck<PoisonQueueLengthCustomCheck>();
        services.AddNServiceBusEndpoint(customChecks, CustomChecksEndpointName);

        // Registered after the endpoints so that the heartbeat endpoint is started before the first heartbeat is sent
        services.AddHostedService<Heartbeat>();
    }

    static EndpointConfiguration CreateSendOnlyEndpoint(string endpointName)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.SendOnly();
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.AssemblyScanner().Disable = true;
        return endpointConfiguration;
    }
}
