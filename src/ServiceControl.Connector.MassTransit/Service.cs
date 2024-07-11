using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;

public class Service(
    ILogger<Service> logger,
    TransportDefinitionFactory transportDefinitionFactory,
    IQueueInformationProvider queueInformationProvider,
    IQueueFilter queueFilter,
    MassTransitFailureAdapter adapter,
    Configuration configuration,
    ReceiverFactory receiverFactory,
    IHostApplicationLifetime hostApplicationLifetime
) : BackgroundService
{
    TransportDefinition? transportDefinition;
    TransportInfrastructure? infrastructure;
    HashSet<string>? massTransitErrorQueues;

#pragma warning disable PS0018
    async Task<HashSet<string>> GetReceiveQueues()
#pragma warning restore PS0018
    {
        try
        {
            var queues = await queueInformationProvider.GetQueues();
            return queues.Where(queueFilter.IsMatch).ToHashSet();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failure querying the queue information");
        }

        return [];
    }

#pragma warning disable PS0017
    protected override async Task ExecuteAsync(CancellationToken shutdownToken = default)
#pragma warning restore PS0017
    {
        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly()!.Location).ProductVersion!;
        logger.LogInformation("ServiceControl.Connector.MassTransit {Version}", version);

        //Setup mode only
        if (configuration.SetupInfrastructure)
        {
            massTransitErrorQueues = await GetReceiveQueues();
            await Setup(shutdownToken);

            logger.LogInformation("Signaling stop as only run in setup mode");
            hostApplicationLifetime.StopApplication();
            return;
        }

        massTransitErrorQueues = [];

        try
        {
            while (!shutdownToken.IsCancellationRequested)
            {
                var newData = await GetReceiveQueues();

                var errorQueuesAreNotTheSame = !newData.SetEquals(massTransitErrorQueues);

                if (errorQueuesAreNotTheSame)
                {
                    logger.LogInformation("Changes detected, restarting");
                    await Teardown(shutdownToken);
                    massTransitErrorQueues = newData;
                    await Setup(shutdownToken);
                }

                await Task.Delay(configuration.QueueScanInterval, shutdownToken);
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

        transportDefinition = transportDefinitionFactory.CreateTransportDefinition();
        infrastructure = await transportDefinition.Initialize(hostSettings, receiverSettings, [], cancellationToken);

        var messageDispatcher = infrastructure.Dispatcher;

        OnMessage forwardMessage = async (messageContext, token) =>
        {
            using var scope = logger.BeginScope("FORWARD {ReceiveAddress} {NativeMessageId}", messageContext.ReceiveAddress, messageContext.NativeMessageId);
            var operation = adapter.ForwardMassTransitErrorToServiceControl(messageContext);
            await messageDispatcher.Dispatch(new TransportOperations(operation), messageContext.TransportTransaction, token);
        };

        OnMessage returnMessage = async (messageContext, token) =>
        {
            using var scope = logger.BeginScope("RETURN {ReceiveAddress} {NativeMessageId}", messageContext.ReceiveAddress, messageContext.NativeMessageId);
            var operation = adapter.ReturnMassTransitFailure(messageContext);
            await messageDispatcher.Dispatch(new TransportOperations(operation), messageContext.TransportTransaction, token);
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
                onError: async (context, token) =>
                {
                    // Maybe instead can we use some native delivery counting or is that already used?
                    logger.LogError(context.Exception, "Discarding due to failure: {ExceptionMessage}",
                        context.Exception.Message);

                    var poisonMessage = new OutgoingMessage(context.Message.MessageId, context.Message.Headers,
                        context.Message.Body);
                    var operation =
                        new TransportOperation(poisonMessage, new UnicastAddressTag(configuration.ErrorQueue));
                    var operations = new TransportOperations(operation);

                    await messageDispatcher.Dispatch(operations, context.TransportTransaction, token);

                    return ErrorHandleResult.Handled;
                }, cancellationToken: cancellationToken);

            await receiver.StartReceive(cancellationToken);
        }
    }

    async Task Teardown(CancellationToken cancellationToken)
    {
        if (infrastructure != null)
        {
            await infrastructure.Shutdown(cancellationToken);
        }

        transportDefinition = null;
    }
}