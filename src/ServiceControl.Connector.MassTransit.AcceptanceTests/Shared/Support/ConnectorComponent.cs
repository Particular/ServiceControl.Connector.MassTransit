using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using ServiceControl.Connector.MassTransit;

public class ConnectorComponent<TContext>(string name, string errorQueue, string returnQueue, string[] queueNamesToMonitor) : IComponentBehavior
    where TContext : ScenarioContext
{
    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        return Task.FromResult<ComponentRunner>(new Runner(name, errorQueue, returnQueue, queueNamesToMonitor, run.ScenarioContext, new AcceptanceTestLoggerFactory(run.ScenarioContext)));
    }

    class StaticQueueNames(string[] queueNames) : IFileBasedQueueInformationProvider
    {
        public Task<IEnumerable<string>> GetQueues(CancellationToken cancellationToken) => Task.FromResult<IEnumerable<string>>(queueNames);
    }

    class Runner(string name, string errorQueue, string returnQueue, string[] queueNamesToMonitor,
        ScenarioContext scenarioContext,
        AcceptanceTestLoggerFactory loggerFactory) : ComponentRunner
    {
        public override string Name { get; } = name;

        public override async Task Start(CancellationToken cancellationToken = default)
        {
            var transportConfig = TestSuiteConfiguration.Current.CreateTransportConfiguration();

            var builder = Host.CreateDefaultBuilder()
                .ConfigureLogging(cfg => cfg.ClearProviders())
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton((TContext)scenarioContext);
                    services.AddSingleton(new Configuration
                    {
                        ReturnQueue = returnQueue,
                        ErrorQueue = errorQueue,
                        Command = Command.SetupAndRun
                    });
                    services.AddSingleton<IUserProvidedQueueNameFilter>(new UserProvidedQueueNameFilter(null));
                    services.AddSingleton<MassTransitConverter>();
                    services.AddSingleton<MassTransitFailureAdapter>();
                    services.AddSingleton<ReceiverFactory>();
                    services.AddSingleton(loggerFactory);
                    services.AddHostedService<Service>();
                    services.AddSingleton(TimeProvider.System);
                    services.AddSingleton<IProvisionQueues, ProvisionQueues>();
                    services.AddSingleton<IFileBasedQueueInformationProvider>(new StaticQueueNames(queueNamesToMonitor));
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

        readonly string errorQueue = errorQueue;
        readonly string returnQueue = returnQueue;
        readonly ScenarioContext scenarioContext = scenarioContext;
        readonly AcceptanceTestLoggerFactory loggerFactory = loggerFactory;
    }
}