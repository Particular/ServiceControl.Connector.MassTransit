using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public static class AdapterAmazonSqsConfiguration
{
    public static void UsingAmazonSqs(this IServiceCollection services)
    {
        services.AddSingleton<IQueueInformationProvider>(new AmazonSqsHelper(string.Empty));
        services.AddSingleton<TransportDefinition>(new SqsTransport { DoNotWrapOutgoingMessages = true });
        services.AddSingleton<MassTransitFailureAdapter, AmazonSqsMassTransitFailureAdapter>();
    }
}
