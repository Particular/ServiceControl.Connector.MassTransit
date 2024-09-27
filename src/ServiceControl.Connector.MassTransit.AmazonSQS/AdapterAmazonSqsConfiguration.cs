using Microsoft.Extensions.DependencyInjection;

public static class AdapterAmazonSqsConfiguration
{
    public static void UsingAmazonSqs(this IServiceCollection services, Action<SqsTransport>? transportConfig = null)
    {
        services.AddSingleton<IQueueInformationProvider>(new AmazonSqsHelper(string.Empty));
        services.AddSingleton(new TransportDefinitionFactory(() =>
        {
            var transport = new SqsTransport(enableDelayedDelivery: false) { DoNotWrapOutgoingMessages = true };
            transportConfig?.Invoke(transport);
            return transport;
        }));
        services.AddSingleton<MassTransitFailureAdapter, AmazonSqsMassTransitFailureAdapter>();
    }
}
