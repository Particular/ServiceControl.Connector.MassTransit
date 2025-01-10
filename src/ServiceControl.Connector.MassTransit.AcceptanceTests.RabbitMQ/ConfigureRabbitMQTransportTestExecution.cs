using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting.Support;

class ConfigureRabbitMQTransportTestExecution : IConfigureTransportTestExecution
{
    public Func<CancellationToken, Task> ConfigureTransportForEndpoint(EndpointConfiguration endpointConfiguration, PublisherMetadata publisherMetadata)
    {
        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum), "host=localhost", false);
        endpointConfiguration.UseTransport(transport);
        return Cleanup;
    }

    public void ConfigureTransportForMassTransitEndpoint(IBusRegistrationConfigurator configurator)
    {
        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("localhost", "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });

            cfg.ConfigureEndpoints(context);
        });

        configurator.AddSingleton<IRetryMessageVerification>(new RabbitMQRetryMessageVerification());

        configurator.AddConfigureEndpointsCallback((name, cfg) =>
        {
            if (cfg is IRabbitMqReceiveEndpointConfigurator rmq)
            {
                rmq.SetQuorumQueue();
            }
        });
    }

    public void ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration)
    {
        services.UsingRabbitMQ("host=localhost", new Uri("http://localhost:15672/"), "guest", "guest");
    }

    Task Cleanup(CancellationToken cancellationToken)
    {
        //TODO?
        return Task.CompletedTask;
    }
}