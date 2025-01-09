using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit;
using ServiceControl.Connector.MassTransit.AcceptanceTesting;

public class ConnectorComponent<TContext>(string name, string errorQueue, string returnQueue, string[] queueNamesToMonitor, string? customCheckQueue) : IComponentBehavior
    where TContext : ScenarioContext
{
    public Task<ComponentRunner> CreateRunner(RunDescriptor run) => Task.FromResult<ComponentRunner>(new Runner(name, errorQueue, returnQueue, queueNamesToMonitor, customCheckQueue, run.ScenarioContext, new ScenarioContextLoggerProvider(run.ScenarioContext)));

    class StaticQueueNames(string[] queueNames) : IFileBasedQueueInformationProvider
    {
        public async IAsyncEnumerable<string> GetQueues([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();

            foreach (var queueName in queueNames)
            {
                yield return queueName;
            }
        }
    }

    class Runner(string name, string errorQueue, string returnQueue, string[] queueNamesToMonitor, string? customCheckQueue,
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
                    var configuration = new Configuration
                    {
                        ReturnQueue = returnQueue,
                        ErrorQueue = errorQueue,
                        QueueScanInterval = TimeSpan.FromSeconds(5),
                        CustomChecksQueue = customCheckQueue ?? "Particular.ServiceControl",
                        Command = Command.SetupAndRun
                    };
                    services.AddSingleton((TContext)scenarioContext);
                    services.AddSingleton(configuration);
                    services.AddSingleton<MassTransitConverter>();
                    services.AddSingleton<MassTransitFailureAdapter>();
                    services.AddSingleton<ReceiverFactory>();
                    services.AddHostedService<Service>();
                    services.AddSingleton<IProvisionQueues, ProvisionQueues>();
                    services.AddSingleton(TimeProvider.System);
                    services.AddSingleton<IFileBasedQueueInformationProvider>(new StaticQueueNames(queueNamesToMonitor));
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