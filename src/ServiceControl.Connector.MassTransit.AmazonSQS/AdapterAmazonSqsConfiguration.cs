using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAmazonSqsConfiguration
{
    public static void UsingAmazonSqs(this IServiceCollection services, Action<SqsTransport>? transportConfig = null)
    {
        services.AddSingleton<IQueueInformationProvider>(services => new AmazonSqsHelper(services.GetRequiredService<IAmazonSQS>(), string.Empty));
        services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
        services.AddSingleton<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();
        services.AddTransient<TransportDefinition>(sp =>
        {
            var sqs = sp.GetRequiredService<IAmazonSQS>();
            var sns = sp.GetRequiredService<IAmazonSimpleNotificationService>();
#pragma warning disable NSBSQSEXP0001
            var transport = new SqsTransport(sqs, sns, enableDelayedDelivery: false)
#pragma warning restore NSBSQSEXP0001
            {
                DoNotWrapOutgoingMessages = true // When forwarding and returning the message we do not want to alter the payload
            };
            transportConfig?.Invoke(transport);
            return transport;
        });
        services.AddSingleton(sp => new TransportInfrastructureFactory(async (hostSettings, receivers, sendingAddresses, cancellationToken) =>
        {
            var transport = sp.GetRequiredService<TransportDefinition>();
            var configuration = sp.GetRequiredService<Configuration>();
            var client = sp.GetRequiredService<IAmazonSQS>();

            var infrastructure = await transport.Initialize(
                hostSettings,
                receivers,
                sendingAddresses,
                cancellationToken
            );

            var dispatcher = new CustomSqsDispatcher(
                client,
                infrastructure.Dispatcher,
                configuration.ErrorQueue
                );

            return new CustomSqsTransportInfrastructure(infrastructure, dispatcher);
        }));
    }
}