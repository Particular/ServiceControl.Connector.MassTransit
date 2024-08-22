using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

public class MassTransitComponent<TContext> : IComponentBehavior
    where TContext : ScenarioContext
{
    public MassTransitComponent(string name, Action<IBusRegistrationConfigurator> busConfig, Action<HostBuilderContext, IServiceCollection> hostConfig)
    {
        this.name = name;
        this.busConfig = busConfig;
        this.hostConfig = hostConfig;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        return Task.FromResult<ComponentRunner>(new Runner(name, busConfig, hostConfig, run.ScenarioContext, new AcceptanceTestLoggerFactory(run.ScenarioContext)));
    }

    readonly string name;
    readonly Action<IBusRegistrationConfigurator> busConfig;
    readonly Action<HostBuilderContext, IServiceCollection> hostConfig;

    class Runner : ComponentRunner
    {
        public Runner(string name,
            Action<IBusRegistrationConfigurator> busConfig,
            Action<HostBuilderContext, IServiceCollection> hostConfig,
            ScenarioContext scenarioContext,
            ILoggerFactory loggerFactory)
        {
            this.busConfig = busConfig;
            this.hostConfig = hostConfig;
            this.scenarioContext = scenarioContext;
            this.loggerFactory = loggerFactory;
            Name = name;
        }

        public override string Name { get; }

        public override async Task Start(CancellationToken cancellationToken = default)
        {
            var transportConfig = TestSuiteConfiguration.Current.CreateTransportConfiguration();

            var builder = Host.CreateDefaultBuilder()
                .ConfigureLogging(cfg => cfg.ClearProviders())
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddMassTransit(x =>
                    {
                        busConfig(x);
                        transportConfig.ConfigureTransportForMassTransitEndpoint(x);
                    });
                    hostConfig(hostContext, services);
                    services.AddSingleton((TContext)scenarioContext);
                });

            host = builder.Build();
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public override async Task Stop(CancellationToken cancellationToken = default)
        {
            if (host is null)
            {
                return;
            }

            try
            {
                await host.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                host.Dispose();
            }
        }

        IHost host;

        readonly Action<IBusRegistrationConfigurator> busConfig;
        readonly Action<HostBuilderContext, IServiceCollection> hostConfig;
        readonly ScenarioContext scenarioContext;
        //TODO: Figure out how to do logging?
#pragma warning disable IDE0052
        readonly ILoggerFactory loggerFactory;
#pragma warning restore IDE0052
    }
}