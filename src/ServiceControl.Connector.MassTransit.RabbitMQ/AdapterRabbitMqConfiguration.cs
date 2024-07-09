using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterRabbitMqConfiguration
{
    public static void UsingRabbitMQ(this IServiceCollection services, Uri managementApi)
    {
        services.AddSingleton<IQueueInformationProvider>(new RabbitMQHelper("/", managementApi));
        services.AddSingleton<TransportDefinition>(new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            "host=localhost",
            enableDelayedDelivery: false, // TODO: Requires https://github.com/Particular/NServiceBus.RabbitMQ/tree/tf527
            enablePublishSubscribe: false // TODO: Requires https://github.com/Particular/NServiceBus.RabbitMQ/tree/tf527
        ));
    }
}