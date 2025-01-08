using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using ServiceControl.Connector.MassTransit;
using ServiceControl.Connector.MassTransit.AcceptanceTesting;

public class ConnectorComponent<TContext>(string name, string errorQueue, string returnQueue) : IComponentBehavior
    where TContext : ScenarioContext
{
    public Task<ComponentRunner> CreateRunner(RunDescriptor run) => Task.FromResult<ComponentRunner>(new Runner(name, errorQueue, returnQueue, run.ScenarioContext, new ScenarioContextLoggerProvider(run.ScenarioContext)));

    class Runner(string name, string errorQueue, string returnQueue,
        ScenarioContext scenarioContext,
        ScenarioContextLoggerProvider loggerProvider) : ComponentRunner
    {
        public override string Name { get; } = name;

        public override async Task Start(CancellationToken cancellationToken = default)
        {
            var transportConfig = TestSuiteConfiguration.Current.CreateTransportConfiguration();

            var builder = Host.CreateDefaultBuilder()
                .ConfigureLogging(cfg => cfg.ClearProviders().AddProvider(loggerProvider))
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton((TContext)scenarioContext);
                    services.AddSingleton(new Configuration
                    {
                        ReturnQueue = returnQueue,
                        ErrorQueue = errorQueue,
                        QueueScanInterval = TimeSpan.FromSeconds(5),
                        Command = Command.SetupAndRun
                    });
                    services.AddSingleton<IUserProvidedQueueNameFilter>(new UserProvidedQueueNameFilter(null));
                    services.AddSingleton<MassTransitConverter>();
                    services.AddSingleton<MassTransitFailureAdapter>();
                    services.AddSingleton<ReceiverFactory>();
                    services.AddHostedService<Service>();
                    services.AddSingleton<IProvisionQueues, ProvisionQueues>();
                    transportConfig.ConfigureTransportForConnector(services, hostContext.Configuration);
                });

            host = builder.Build();

            var provisionQueues = host.Services.GetRequiredService<IProvisionQueues>();
            await provisionQueues.TryProvision(cancellationToken);

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