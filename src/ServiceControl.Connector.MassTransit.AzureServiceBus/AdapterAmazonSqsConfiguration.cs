using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAzureServiceBusConfiguration
{
  public static void UsingAzureServiceBus(this IServiceCollection services)
  {
    var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRING_AZURESERVICEBUS")
                           ?? throw new InvalidOperationException("Envvar CONNECTIONSTRING_AZURESERVICEBUS not set");

    services.AddSingleton<IQueueInformationProvider>(new AzureServiceBusHelper(connectionString));
    services.AddSingleton<TransportDefinition>(
      new AzureServiceBusTransport(connectionString)
      {
        TransportTransactionMode = TransportTransactionMode.ReceiveOnly
      });
  }
}
