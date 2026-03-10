using System;
using System.Collections.Generic;
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

    Func<IReadOnlyCollection<string>, CancellationToken, Task> ConfigureTransportForMassTransitEndpoint(IBusRegistrationConfigurator configurator);

    Func<IReadOnlyCollection<string>, CancellationToken, Task> ConfigureTransportForConnector(IServiceCollection services, IConfiguration configuration);
}