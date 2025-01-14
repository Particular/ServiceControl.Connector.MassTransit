using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Transport;

public class CustomCheckReporter : IHostedService
{
    TransportDefinition transportDefinition;
    Configuration config;
    IHostApplicationLifetime applicationLifetime;
    IQueueLengthProvider queueLengthProvider;
    IEndpointInstance? endpoint;

    public CustomCheckReporter(
        TransportDefinition transportDefinition,
        IQueueLengthProvider queueLengthProvider,
        Configuration config,
        IHostApplicationLifetime applicationLifetime
    )
    {
        this.transportDefinition = transportDefinition;
        this.config = config;
        this.applicationLifetime = applicationLifetime;
        this.queueLengthProvider = queueLengthProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpointConfig = new EndpointConfiguration(nameof(CustomCheckReporter));
        endpointConfig.SendOnly();
        endpointConfig.ReportCustomChecksTo(config.ServiceControlQueue);
        endpointConfig.UseSerialization<SystemJsonSerializer>();
        endpointConfig.RegisterComponents(collection =>
        {
            collection.AddSingleton(config);
            collection.AddSingleton(applicationLifetime);
            collection.AddSingleton(queueLengthProvider);
        });

        endpointConfig.UseTransport(transportDefinition);
        endpoint = await Endpoint.Start(endpointConfig, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (endpoint != null)
        {
            await endpoint.Stop(cancellationToken);
        }
    }
}