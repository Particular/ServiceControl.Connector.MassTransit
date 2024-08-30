using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting.Support;

class ConfigureAzureServiceBusTransportTestExecution : IConfigureTransportTestExecution
{
    readonly string connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString")!;

    public Func<CancellationToken, Task> ConfigureTransportForEndpoint(EndpointConfiguration endpointConfiguration, PublisherMetadata publisherMetadata)
    {
        var transport = new AzureServiceBusTransport(connectionString);
        endpointConfiguration.UseTransport(transport);
        return Cleanup;
    }

    public void ConfigureTransportForMassTransitEndpoint(IBusRegistrationConfigurator configurator)
    {
        configurator.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(connectionString);
            cfg.ConfigureEndpoints(context);
        });
    }

    public void ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration)
    {
        services.UsingAzureServiceBus(configuration, connectionString);
        services.AddSingleton<IQueueFilter>(new AcceptanceTestQueueFilter());
    }

    Task Cleanup(CancellationToken cancellationToken)
    {
        //TODO?
        return Task.CompletedTask;
    }
}