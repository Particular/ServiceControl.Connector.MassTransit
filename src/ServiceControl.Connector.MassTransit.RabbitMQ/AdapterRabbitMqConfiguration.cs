using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterRabbitMqConfiguration
{
    public static void UsingRabbitMQ(this IServiceCollection services, string connectionString, Uri managementApi)
    {
        var rabbitMqHelper = new RabbitMQHelper("/", managementApi);
        services.AddSingleton<IQueueInformationProvider>(rabbitMqHelper);
        services.AddSingleton<IQueueLengthProvider>(rabbitMqHelper);
        services.AddTransient<TransportDefinition>(sp => new RabbitMQTransport(
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
        });
        services.AddSingleton(sp => new TransportInfrastructureFactory(async (HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken) =>
        {
            var transport = sp.GetRequiredService<TransportDefinition>();
            return await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
        }));
        services.AddSingleton<MassTransitFailureAdapter, RabbitMQMassTransitFailureAdapter>();
    }
}