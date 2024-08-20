using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public class Service(
    ILogger<Service> logger,
    TransportDefinition transportDefinition,
    IQueueInformationProvider queueInformationProvider,
    IQueueFilter queueFilter,
    MassTransitFailureAdapter adapter,
    Configuration configuration,
    ReceiverFactory receiverFactory,
    IHostApplicationLifetime hostApplicationLifetime
) : BackgroundService
{
    TransportInfrastructure? infrastructure;
    HashSet<string>? massTransitErrorQueues;

#pragma warning disable PS0018
    async Task<HashSet<string>> GetReceiveQueues()
#pragma warning restore PS0018
    {
        var queues = await queueInformationProvider.GetQueues();
        return queues.Where(queueFilter.IsMatch).ToHashSet();
    }

#pragma warning disable PS0017
    protected override async Task ExecuteAsync(CancellationToken shutdownToken = default)
#pragma warning restore PS0017
    {
        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly()!.Location).ProductVersion!;
        logger.LogInformation("ServiceControl.Connector.MassTransit {Version}", version);

        massTransitErrorQueues = await GetReceiveQueues();
        await Setup(shutdownToken);

        if (configuration.SetupInfrastructure)
        {
            logger.LogInformation("Signaling stop as only run in setup mode");
            hostApplicationLifetime.StopApplication();
            return;
        }

        try
        {
            while (!shutdownToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), shutdownToken);

                var newData = await GetReceiveQueues();

                var setEquals = newData.SetEquals(massTransitErrorQueues);

                if (!setEquals)
                {
                    logger.LogInformation("Changes detected, restarting");
                    await Teardown(shutdownToken);
                    massTransitErrorQueues = newData;
                    await Setup(shutdownToken);
                }
            }
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            // Ignore
        }

        await StopAsync(shutdownToken);
    }

    async Task Setup(CancellationToken cancellationToken)
    {
        var hostSettings = new HostSettings(
            name: configuration.ReturnQueue,
            hostDisplayName: configuration.ReturnQueue,
            startupDiagnostic: new StartupDiagnosticEntries(),
            criticalErrorAction: (_, exception, _) =>
            {
                logger.LogCritical(exception, "Critical error, signaling to stop host");
                hostApplicationLifetime.StopApplication();
            },
            true
        );

        var receiveSettings = new List<ReceiveSettings>
        {
            new(
                id: "Return",
                receiveAddress: new QueueAddress(configuration.ReturnQueue),
                usePublishSubscribe: false,
                purgeOnStartup: false,
                errorQueue: configuration.PoisonQueue
            )
        };

        foreach (var massTransitErrorQueue in massTransitErrorQueues!)
        {
            logger.LogInformation("listening to: {InputQueue}", massTransitErrorQueue);
            receiveSettings.Add(receiverFactory.Create(massTransitErrorQueue, configuration.ErrorQueue));
        }

        if (!receiveSettings.Any())
        {
            throw new InvalidOperationException("No input queues discovered");
        }

        var receiverSettings = receiveSettings.ToArray();

        infrastructure = await transportDefinition.Initialize(hostSettings, receiverSettings, [], cancellationToken);

        var messageDispatcher = infrastructure.Dispatcher;

        OnMessage forwardMessage = async (messageContext, token) =>
        {
            using var scope = logger.BeginScope("FORWARD {ReceiveAddress} {NativeMessageId}", messageContext.ReceiveAddress, messageContext.NativeMessageId);
            var operation = adapter.ForwardMassTransitErrorToServiceControl(messageContext);
            await messageDispatcher.Dispatch(new TransportOperations(operation), messageContext.TransportTransaction, token);
        };

        OnMessage returnMessage = async (context, token) =>
        {
            using var scope = logger.BeginScope("RETURN {ReceiveAddress} {NativeMessageId}", context.ReceiveAddress, context.NativeMessageId);
            var operation = adapter.ReturnMassTransitFailure(context);
            await messageDispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, token);

            // TODO: Add error handling to forward message to an error/poison queue, maybe even put it back in the same queue "at the end" or maybe just have a circuit breaker and delay 
        };

        if (configuration.SetupInfrastructure)
        {
            logger.LogInformation("Not starting receivers as in setup mode");
            return;
        }

        foreach (var receiverSetting in receiverSettings)
        {
            OnMessage onMessage = receiverSetting.Id == "Return"
                ? returnMessage
                : forwardMessage;

            var receiver = infrastructure.Receivers[receiverSetting.Id];
            await receiver.Initialize(new PushRuntimeSettings(1),
                onMessage: (context, token) => onMessage(context, token),
                onError: (context, _) =>
                {
                    logger.LogError(context.Exception, "Discarding due to failure: {ExceptionMessage}",
                        context.Exception.Message);
                    // TODO: Smarter handling
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }, cancellationToken: cancellationToken);

            await receiver.StartReceive(cancellationToken);
        }
    }

    async Task Teardown(CancellationToken cancellationToken)
    {
        await infrastructure!.Shutdown(cancellationToken);
    }
}