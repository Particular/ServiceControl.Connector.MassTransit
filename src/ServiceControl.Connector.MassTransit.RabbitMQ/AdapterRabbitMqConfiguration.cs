using Microsoft.Extensions.DependencyInjection;

public static class AdapterRabbitMqConfiguration
{
    public static void UsingRabbitMQ(this IServiceCollection services, string connectionString, Uri managementApi)
    {
        services.AddSingleton<IQueueInformationProvider>(new RabbitMQHelper("/", managementApi));
        services.AddSingleton(new TransportDefinitionFactory(() =>
        {
            var transport = new RabbitMQTransport(
                RoutingTopology.Conventional(QueueType.Quorum),
                connectionString,
                enableDelayedDelivery: false
            )
            {
                OutgoingNativeMessageCustomization = (operation, properties) =>
                {
                    if (operation.Properties.TryGetValue(MassTransitFailureAdapter.ContentTypeKey, out var contentType))
                    {
                        properties.ContentType = contentType;
                    }
                }
            };
            return transport;
        }));
        services.AddSingleton<MassTransitFailureAdapter, RabbitMQMassTransitFailureAdapter>();
    }
}