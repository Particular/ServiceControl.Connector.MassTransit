using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
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
        services.AddSingleton(new TransportDefinitionFactory(() => new AzureServiceBusTransport(connectionString) { TransportTransactionMode = TransportTransactionMode.ReceiveOnly, DoNotSendTransportEncodingHeader = true, OutgoingNativeMessageCustomization = OutgoingNativeMessageCustomization }));
        services.AddSingleton<ReceiverFactory>(new AzureServiceBusReceiverFactory(receiveMode));
    }

    static void OutgoingNativeMessageCustomization(IOutgoingTransportOperation operation, ServiceBusMessage message)
    {
    }
}