using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public static class AdapterAzureServiceBusConfiguration
{
    public static void UsingAzureServiceBus(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        var receiveMode = configuration.GetValue<SubQueue?>(nameof(SubQueue)) ?? SubQueue.None;

        services.AddSingleton<IQueueInformationProvider>(b => new AzureServiceBusHelper(b.GetRequiredService<ILogger<AzureServiceBusHelper>>(), connectionString));
        services.AddSingleton<IQueueLengthProvider>(b => new AzureServiceBusHelper(b.GetRequiredService<ILogger<AzureServiceBusHelper>>(), connectionString));
        services.AddTransient<TransportDefinition>(sp => new AzureServiceBusTransport(connectionString)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly,
            OutgoingNativeMessageCustomization = OutgoingNativeMessageCustomization,
#pragma warning disable CS0618 // Type or member is obsolete
            DoNotSendTransportEncodingHeader = true
#pragma warning restore CS0618 // Type or member is obsolete
        });
        services.AddSingleton(sp => new TransportInfrastructureFactory(async (HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken) =>
        {
            var transport = sp.GetRequiredService<TransportDefinition>();
            return await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
        }));
        services.AddSingleton<ReceiverFactory>(new AzureServiceBusReceiverFactory(receiveMode));
    }

    static void OutgoingNativeMessageCustomization(IOutgoingTransportOperation operation, ServiceBusMessage message)
    {
        var p = operation.Properties;
        if (p.TryGetValue(MassTransitFailureAdapter.MessageIdKey, out var messageId))
        {
            message.MessageId = messageId;
        }

        if (p.TryGetValue(MassTransitFailureAdapter.ContentTypeKey, out var contentType))
        {
            message.ContentType = contentType;
        }
    }
}