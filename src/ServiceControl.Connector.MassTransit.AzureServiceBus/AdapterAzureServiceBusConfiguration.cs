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
        services.AddSingleton(new TransportDefinitionFactory(() =>
            new AzureServiceBusTransport(connectionString) { TransportTransactionMode = TransportTransactionMode.ReceiveOnly, DoNotSendTransportEncodingHeader = true, OutgoingNativeMessageCustomization = OutgoingNativeMessageCustomization, ServiceBusProcessorOptionsCustomizer = ServiceBusProcessorOptionsCustomizer }));

        services.AddSingleton<ReceiverFactory>(new AzureServiceBusReceiverFactory(receiveMode));
    }

    static void ServiceBusProcessorOptionsCustomizer(ReceiveSettings receiveSettings, ServiceBusProcessorOptions processorOptions)
    {
        var isDlq = receiveSettings.ReceiveAddress.Properties[nameof(ReceiveMode)] == ReceiveMode.DeadLetterQueue.ToString();
        if (isDlq)
        {
            processorOptions.SubQueue = SubQueue.DeadLetter;
        }
    }

    static void OutgoingNativeMessageCustomization(IOutgoingTransportOperation operation, ServiceBusMessage message)
    {
    }
}