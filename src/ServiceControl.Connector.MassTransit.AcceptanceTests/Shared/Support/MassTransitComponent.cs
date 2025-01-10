using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using ServiceControl.Connector.MassTransit.AcceptanceTesting;

public class MassTransitComponent<TContext>(string name, Action<IBusRegistrationConfigurator> busConfig, Action<HostBuilderContext, IServiceCollection> hostConfig) : IComponentBehavior
    where TContext : ScenarioContext
{
    public Task<ComponentRunner> CreateRunner(RunDescriptor run) => Task.FromResult<ComponentRunner>(new Runner(name, busConfig, hostConfig, run.ScenarioContext, new ScenarioContextLoggerProvider(run.ScenarioContext)));

    class Runner(string name,
        Action<IBusRegistrationConfigurator> busConfig,
        Action<HostBuilderContext, IServiceCollection> hostConfig,
        ScenarioContext scenarioContext,
        ScenarioContextLoggerProvider loggerProvider) : ComponentRunner
    {
        public override string Name { get; } = name;

        public override async Task Start(CancellationToken cancellationToken = default)
        {
            var transportConfig = TestSuiteConfiguration.Current.CreateTransportConfiguration();

            var builder = Host.CreateDefaultBuilder()
                .ConfigureLogging(cfg => cfg.ClearProviders().SetMinimumLevel(LogLevel.Debug).AddProvider(loggerProvider))
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

        IHost? host;
    }
}