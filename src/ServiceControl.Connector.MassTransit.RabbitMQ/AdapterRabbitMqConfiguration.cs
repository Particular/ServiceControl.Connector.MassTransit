using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterRabbitMqConfiguration
{
    public static void UsingRabbitMQ(this IServiceCollection services, string connectionString, Uri managementApi)
    {
        services.AddSingleton<IQueueInformationProvider>(new RabbitMQHelper("/", managementApi));
        services.AddSingleton(new TransportInfrastructureFactory(async (HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken) =>
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
            return await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
        }));
        services.AddSingleton<MassTransitFailureAdapter, RabbitMQMassTransitFailureAdapter>();
    }
}