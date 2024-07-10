using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public static class AdapterAzureServiceBusConfiguration
{
    public static void UsingAzureServiceBus(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        var receiveMode = configuration.GetValue<ReceiveMode?>("ReceiveMode") ?? ReceiveMode.Queue;

        if (receiveMode == ReceiveMode.DeadLetterQueue)
        {
            services.AddSingleton<IQueueFilter, AllQueueFilter>();
        }

        services.AddSingleton<IQueueInformationProvider>(b => new AzureServiceBusHelper(b.GetRequiredService<ILogger<AzureServiceBusHelper>>(), connectionString));
        services.AddSingleton<TransportDefinition>(
            new AzureServiceBusTransport(connectionString)
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            });
        services.AddSingleton<ReceiverFactory>(new AzureServiceBusReceiverFactory(receiveMode));
        services.AddSingleton<MassTransitFailureAdapter, AzureServiceBusMassTransitFailureAdapter>();
    }
}