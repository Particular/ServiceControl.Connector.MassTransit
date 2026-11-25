using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddTransient(sp => CreateTransport(
            sp.GetRequiredService<IAmazonSQS>(),
            sp.GetRequiredService<IAmazonSimpleNotificationService>(),
            transportConfig));
        // Endpoints hosted by the connector are created before the service provider exists, so they get their own clients
        services.AddTransportDefinition(() => CreateTransport(new AmazonSQSClient(), new AmazonSimpleNotificationServiceClient(), transportConfig));
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

    static SqsTransport CreateTransport(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, Action<SqsTransport>? transportConfig)
    {
        var transport = new SqsTransport(sqs, sns, disableUnrestrictedDelayedDelivery: true)
        {
            DoNotWrapOutgoingMessages = true // When forwarding and returning the message we do not want to alter the payload
        };
        transportConfig?.Invoke(transport);
        return transport;
    }
}
