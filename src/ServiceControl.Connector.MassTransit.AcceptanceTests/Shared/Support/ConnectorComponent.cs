using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit;

public class ConnectorComponent<TContext> : IComponentBehavior
    where TContext : ScenarioContext
{
    public ConnectorComponent(string name, string errorQueue, string returnQueue, string? customCheckQueue)
    {
        this.name = name;
        this.errorQueue = errorQueue;
        this.returnQueue = returnQueue;
        this.customCheckQueue = customCheckQueue;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        return Task.FromResult<ComponentRunner>(new Runner(name, errorQueue, returnQueue, customCheckQueue, run.ScenarioContext, new AcceptanceTestLoggerFactory(run.ScenarioContext)));
    }

    readonly string name;
    readonly string errorQueue;
    readonly string returnQueue;
    readonly string? customCheckQueue;

    class Runner : ComponentRunner
    {
        public Runner(string name,
            string errorQueue,
            string returnQueue,
            string? customCheckQueue,
            ScenarioContext scenarioContext,
            AcceptanceTestLoggerFactory loggerFactory)
        {
            this.errorQueue = errorQueue;
            this.returnQueue = returnQueue;
            this.customCheckQueue = customCheckQueue;
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
                    services.AddSingleton((TContext)scenarioContext);
                    var configuration = new Configuration
                    {
                        ReturnQueue = returnQueue,
                        ErrorQueue = errorQueue,
                        CustomChecksQueue = customCheckQueue ?? "Particular.ServiceControl",
                        Command = Command.SetupAndRun,
                        QueueScanInterval = TimeSpan.FromSeconds(5),
                        CustomChecksInterval = TimeSpan.FromSeconds(5)
                    };
                    services.AddSingleton(configuration);
                    services.AddSingleton<IUserProvidedQueueNameFilter>(new UserProvidedQueueNameFilter(null));
                    services.AddSingleton<Service>();
                    services.AddSingleton<MassTransitConverter>();
                    services.AddSingleton<MassTransitFailureAdapter>();
                    services.AddSingleton<ReceiverFactory>();
                    services.AddSingleton(loggerFactory);
                    services.AddHostedService(p => p.GetRequiredService<Service>());
                    if (customCheckQueue != null)
                    {
                        services.AddHostedService<CustomCheckReporter>(provider =>
                            new CustomCheckReporter(
                                provider.GetRequiredService<TransportDefinition>(),
                                provider.GetRequiredService<IQueueLengthProvider>(),
                                configuration,
                                provider.GetRequiredService<IHostApplicationLifetime>()));
                    }
                    transportConfig.ConfigureTransportForConnector(services, hostContext.Configuration);
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

        readonly string errorQueue;
        readonly string returnQueue;
        readonly string? customCheckQueue;
        readonly ScenarioContext scenarioContext;
        readonly AcceptanceTestLoggerFactory loggerFactory;
    }
}