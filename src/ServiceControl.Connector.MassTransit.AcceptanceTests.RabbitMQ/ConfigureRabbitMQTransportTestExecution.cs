using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit.AcceptanceTests.RabbitMQ;

class ConfigureRabbitMQTransportTestExecution(QueueType queueType = QueueType.Quorum) : IConfigureTransportTestExecution
{
    TestRabbitMQTransport? transport;

    public Func<CancellationToken, Task> ConfigureTransportForEndpoint(EndpointConfiguration endpointConfiguration, PublisherMetadata publisherMetadata)
    {
        transport = new TestRabbitMQTransport(RoutingTopology.Conventional(queueType), "host=localhost", false);
        endpointConfiguration.UseTransport(transport);
        return Cleanup;
    }

    public Func<IReadOnlyCollection<string>, CancellationToken, Task> ConfigureTransportForMassTransitEndpoint(IBusRegistrationConfigurator configurator)
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
                if (queueType == QueueType.Quorum)
                {
                    rmq.SetQuorumQueue();
                }
            }
        });

        return DeleteQueues;
    }

    public Func<IReadOnlyCollection<string>, CancellationToken, Task> ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration)
    {
        services.UsingRabbitMQ("host=localhost", new Uri("http://localhost:15672/"), "guest", "guest", queueType);
        return DeleteQueues;
    }

    Task Cleanup(CancellationToken cancellationToken) =>
        transport == null
            ? Task.CompletedTask
            : DeleteQueues(transport.QueuesToCleanup.ToHashSet(), cancellationToken);

    static async Task DeleteQueues(IReadOnlyCollection<string> queues, CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync("Test Queue Purger", cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        foreach (var queue in queues)
        {
            try
            {
                await channel.QueueDeleteAsync(queue, false, false, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
            }
        }
    }

    class TestRabbitMQTransport(RoutingTopology routingTopology, string connectionString, bool enableDelayedDelivery) : RabbitMQTransport(routingTopology, connectionString, enableDelayedDelivery)
    {
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            var infrastructure = await base.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
            QueuesToCleanup.AddRange(infrastructure.Receivers.Select(x => x.Value.ReceiveAddress).Concat(sendingAddresses).Distinct());
            return infrastructure;
        }

        public List<string> QueuesToCleanup { get; } = [];
    }
}