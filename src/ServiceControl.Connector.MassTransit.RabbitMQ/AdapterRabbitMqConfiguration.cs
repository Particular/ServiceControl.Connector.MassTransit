using Microsoft.Extensions.DependencyInjection;

public static class AdapterRabbitMqConfiguration
{
    public static void UsingRabbitMQ(this IServiceCollection services, string connectionString, Uri managementApi)
    {
        services.AddSingleton<IQueueInformationProvider>(new RabbitMQHelper("/", managementApi));
        services.AddSingleton(new TransportDefinitionFactory(() => new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            connectionString,
            enableDelayedDelivery: false
        )));
        services.AddSingleton<MassTransitFailureAdapter, RabbitMQMassTransitFailureAdapter>();
    }
}