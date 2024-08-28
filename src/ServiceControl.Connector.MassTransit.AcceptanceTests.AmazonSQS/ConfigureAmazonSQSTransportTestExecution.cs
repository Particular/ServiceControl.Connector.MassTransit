using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting.Support;

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
        var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var region = Environment.GetEnvironmentVariable("AWS_REGION");

        configurator.UsingAmazonSqs((context, cfg) =>
        {
            cfg.Host(region, h =>
            {
                h.AccessKey(accessKeyId);
                h.SecretKey(secretAccessKey);

                h.Scope(NamePrefixGenerator.GetNamePrefix(), true);
            });

            cfg.ConfigureEndpoints(context, new DefaultEndpointNameFormatter(NamePrefixGenerator.GetNamePrefix(), false));
        });
    }

    public void ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration)
    {
        services.UsingAmazonSqs();
    }
}
