using Amazon.Runtime;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;

class ConfigureAmazonSQSTransportTestExecution : IConfigureTransportTestExecution
{
    public Func<CancellationToken, Task> ConfigureTransportForEndpoint(EndpointConfiguration endpointConfiguration, PublisherMetadata publisherMetadata)
    {
        var transport = new TestableSQSTransport(NamePrefixGenerator.GetNamePrefix());
        endpointConfiguration.UseTransport(transport);
        return _ => Task.CompletedTask;
    }

    public void ConfigureTransportForMassTransitEndpoint(IBusRegistrationConfigurator configurator)
    {
        var region = FallbackRegionFactory.GetRegionEndpoint().SystemName;

        configurator.UsingAmazonSqs((context, cfg) =>
        {
            cfg.Host(region, h =>
            {
                h.Credentials(FallbackCredentialsFactory.GetCredentials());

                h.Scope(NamePrefixGenerator.GetNamePrefix(), true);
            });

            cfg.ConfigureEndpoints(context, new DefaultEndpointNameFormatter(NamePrefixGenerator.GetNamePrefix(), false));
        });
    }

    public void ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration)
    {
        services.UsingAmazonSqs(transport =>
        {
            transport.QueueNamePrefix = NamePrefixGenerator.GetNamePrefix();
            transport.TopicNamePrefix = NamePrefixGenerator.GetNamePrefix();
            transport.QueueNameGenerator = TestNameHelper.GetSqsQueueName;
        });
        services.AddSingleton<IQueueFilter>(new AcceptanceTestQueueFilter());
    }
}