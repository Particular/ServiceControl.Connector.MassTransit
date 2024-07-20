using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterRabbitMqConfiguration
{
    public static void UsingRabbitMQ(this IServiceCollection services, string connectionString, Uri managementApi)
    {
        services.AddSingleton<IQueueInformationProvider>(new RabbitMQHelper("/", managementApi));
        services.AddSingleton<TransportDefinition>(new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            connectionString,
            enableDelayedDelivery: false, // TODO: Requires https://github.com/Particular/NServiceBus.RabbitMQ/tree/tf527
            enablePublishSubscribe: false // TODO: Requires https://github.com/Particular/NServiceBus.RabbitMQ/tree/tf527
        ));
    }
}