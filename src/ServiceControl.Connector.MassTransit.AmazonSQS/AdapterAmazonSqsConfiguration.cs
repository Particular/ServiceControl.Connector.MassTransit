using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAmazonSqsConfiguration
{
    public static void UsingAmazonSqs(this IServiceCollection services, Action<SqsTransport>? transportConfig = null)
    {
        services.AddSingleton(provider => new AmazonSqsHelper(provider.GetRequiredService<IAmazonSQS>(), provider.GetRequiredService<SqsTransport>(), string.Empty));
        services.AddSingleton<IQueueInformationProvider>(provider => provider.GetRequiredService<AmazonSqsHelper>());
        services.AddSingleton<IQueueLengthProvider>(provider => provider.GetRequiredService<AmazonSqsHelper>());
        services.AddSingleton<IHealthCheckerProvider>(provider => provider.GetRequiredService<AmazonSqsHelper>());
        services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
        services.AddSingleton<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();
        services.AddTransient<SqsTransport>(sp =>
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
        services.AddTransient<TransportDefinition>(sp => sp.GetRequiredService<SqsTransport>());
        services.AddSingleton(sp => new TransportInfrastructureFactory(async (hostSettings, receivers, sendingAddresses, cancellationToken) =>
        {
            var transport = sp.GetRequiredService<SqsTransport>();
            var configuration = sp.GetRequiredService<Configuration>();
            var client = sp.GetRequiredService<IAmazonSQS>();

            var infrastructure = await transport.Initialize(
                hostSettings,
                receivers,
                sendingAddresses,
                cancellationToken
            );

            var dispatcher = new CustomSqsDispatcher(
                transport,
                client,
                infrastructure.Dispatcher,
                configuration.ErrorQueue
                );

            return new CustomSqsTransportInfrastructure(infrastructure, dispatcher);
        }));
    }
}