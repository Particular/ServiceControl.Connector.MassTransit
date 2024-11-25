using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

public interface IConfigureTransportTestExecution
{
    Func<CancellationToken, Task> ConfigureTransportForEndpoint(EndpointConfiguration endpointConfiguration, PublisherMetadata publisherMetadata);

    void ConfigureTransportForMassTransitEndpoint(IBusRegistrationConfigurator configurator);

    void ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration);
}