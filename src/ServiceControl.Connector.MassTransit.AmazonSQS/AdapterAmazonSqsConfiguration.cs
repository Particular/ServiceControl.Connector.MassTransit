using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAmazonSqsConfiguration
{
    public static void UsingAmazonSqs(this IServiceCollection services, Action<SqsTransport>? transportConfig = null)
    {
        services.AddSingleton<IQueueInformationProvider>(new AmazonSqsHelper(string.Empty));
        services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
        services.AddSingleton<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();
        services.AddSingleton(sp => new TransportDefinitionFactory(async (HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken) =>
        {
            var sqs = sp.GetRequiredService<IAmazonSQS>();
            var sns = sp.GetRequiredService<IAmazonSimpleNotificationService>();
#pragma warning disable NSBSQSEXP0001
            var transport = new SqsTransport(sqs, sns, enableDelayedDelivery: false) { DoNotWrapOutgoingMessages = true };
#pragma warning restore NSBSQSEXP0001

            transportConfig?.Invoke(transport);

            var infrastructure = await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);

            var configuration = sp.GetRequiredService<Configuration>();

            return new Wrapper(infrastructure, new CustomSQSDispatcher(sp.GetRequiredService<IAmazonSQS>(), infrastructure.Dispatcher, configuration.ErrorQueue));
        }));
    }
}