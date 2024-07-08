using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterRabbitMqConfiguration
{
  public static void UsingRabbitMQ(this IServiceCollection services)
  {
    services.AddSingleton<IQueueInformationProvider>(new RabbitMQHelper("guest", "guest", "/", "http://localhost:15672"));
    services.AddSingleton<TransportDefinition>(new RabbitMQTransport(
      RoutingTopology.Conventional(QueueType.Quorum),
      "host=localhost",
      enableDelayedDelivery: false, // TODO: Requires https://github.com/Particular/NServiceBus.RabbitMQ/tree/tf527
      enablePublishSubscribe: false // TODO: Requires https://github.com/Particular/NServiceBus.RabbitMQ/tree/tf527
      ));
  }
}
