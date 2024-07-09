using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAzureServiceBusConfiguration
{
    public static void UsingAzureServiceBus(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IQueueInformationProvider>(new AzureServiceBusHelper(connectionString));
        services.AddSingleton<TransportDefinition>(
            new AzureServiceBusTransport(connectionString)
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            });
        services.AddSingleton<ReceiverFactory, AzureServiceBusReceiverFactory>();
        services.AddSingleton<MassTransitFailureAdapter, AzureServiceBusMassTransitFailureAdapter>();
    }
}