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
        configurator.UsingAmazonSqs((context, cfg) =>
        {
        });
    }

    public void ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration)
    {
        services.UsingAmazonSqs();
    }
}
