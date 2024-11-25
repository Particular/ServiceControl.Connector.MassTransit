using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit;

public class Service(
    ILogger<Service> logger,
    TransportDefinitionFactory transportDefinitionFactory,
    IQueueInformationProvider queueInformationProvider,
    IQueueFilter queueFilter,
    IUserProvidedQueueNameFilter userQueueNameFilter,
    MassTransitFailureAdapter adapter,
    Configuration configuration,
    ReceiverFactory receiverFactory,
    IHostApplicationLifetime hostApplicationLifetime
) : BackgroundService
{
    TransportDefinition? transportDefinition;
    TransportInfrastructure? infrastructure;
    HashSet<string>? massTransitErrorQueues;

    async Task<HashSet<string>> GetReceiveQueues(CancellationToken cancellationToken)
    {
        try
        {
            var queues = await queueInformationProvider.GetQueues(cancellationToken);
            return queues
                .Where(queueFilter.IsMatch)
                .Where(userQueueNameFilter.IsMatch)
                .ToHashSet();
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

        //Perform setup
        if (configuration.IsSetup)
        {
            massTransitErrorQueues = await GetReceiveQueues(shutdownToken);

            await Setup(shutdownToken);

            if (!configuration.IsRun)
            {
                logger.LogInformation("Signaling stop as only run in setup mode");
                hostApplicationLifetime.StopApplication();
                return;
            }
        }

        massTransitErrorQueues = [];

        try
        {
            while (!shutdownToken.IsCancellationRequested)
            {
                var newData = await GetReceiveQueues(shutdownToken);

                var errorQueuesAreNotTheSame = !newData.SetEquals(massTransitErrorQueues);

                if (errorQueuesAreNotTheSame)
                {
                    logger.LogInformation("Changes detected, restarting");
                    await StopReceiving(shutdownToken);
                    massTransitErrorQueues = newData;
                    await StartReceiving(shutdownToken);
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

        var receiverSettings = receiveSettings.ToArray();

        transportDefinition = transportDefinitionFactory.CreateTransportDefinition();
        infrastructure = await transportDefinition.Initialize(hostSettings, receiverSettings, [], cancellationToken);
    }


    async Task StartReceiving(CancellationToken cancellationToken)
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
            false
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
            TransportOperation operation;
            try
            {
                operation = adapter.ForwardMassTransitErrorToServiceControl(messageContext);
            }
            catch (Exception e)
            {
                throw new ConversionException("Conversion to ServiceControl failed", e);
            }
            await messageDispatcher.Dispatch(new TransportOperations(operation), messageContext.TransportTransaction, token);
        };

        OnMessage returnMessage = async (messageContext, token) =>
        {
            using var scope = logger.BeginScope("RETURN {ReceiveAddress} {NativeMessageId}", messageContext.ReceiveAddress, messageContext.NativeMessageId);
            TransportOperation operation;
            try
            {
                operation = adapter.ReturnMassTransitFailure(messageContext);
            }
            catch (Exception e)
            {
                throw new ConversionException("Conversion from ServiceControl failed", e);
            }
            await messageDispatcher.Dispatch(new TransportOperations(operation), messageContext.TransportTransaction, token);
        };

        foreach (var receiverSetting in receiverSettings)
        {
            OnMessage onMessage = receiverSetting.Id == "Return"
                ? returnMessage
                : forwardMessage;

            var receiver = infrastructure.Receivers[receiverSetting.Id];
            await receiver.Initialize(new PushRuntimeSettings(),
                onMessage: (context, token) => onMessage(context, token),
                onError: async (context, token) =>
                {
                    using var scope = logger.BeginScope("OnError {NativeMessageId}", context.Message.NativeMessageId);
                    var isPoison = context.Exception is ConversionException;

                    var exceedsRetryThreshold = context.ImmediateProcessingFailures > configuration.MaxRetries;

                    // TODO: Add transport specific exception handling as certain exceptions are not recoverable

                    if (isPoison || exceedsRetryThreshold)
                    {
                        logger.LogError(context.Exception, "Moved message to {QueueName}", configuration.ErrorQueue);

                        var poisonMessage = new OutgoingMessage(
                            context.Message.MessageId,
                            context.Message.Headers,
                            context.Message.Body
                        );
                        var address = new UnicastAddressTag(configuration.ErrorQueue);
                        var operation = new TransportOperation(poisonMessage, address);
                        var operations = new TransportOperations(operation);

                        await messageDispatcher.Dispatch(operations, context.TransportTransaction, token);
                        return ErrorHandleResult.Handled;
                    }
                    else
                    {
                        // Exponential back-off with jitter
                        var millisecondsDelay = (int)Math.Min(30000, 100 * Math.Pow(2, context.ImmediateProcessingFailures));
                        millisecondsDelay += Random.Shared.Next(millisecondsDelay / 5);

                        logger.LogWarning(context.Exception, "Retrying message {QueueName}, attempt {ImmediateProcessingFailures} of {MaxRetries} after {millisecondsDelay} milliseconds", configuration.ErrorQueue, context.ImmediateProcessingFailures, configuration.MaxRetries, millisecondsDelay);
                        await Task.Delay(millisecondsDelay, token);
                        return ErrorHandleResult.RetryRequired;
                    }
                }, cancellationToken: cancellationToken);

            await receiver.StartReceive(cancellationToken);
        }
    }

    async Task StopReceiving(CancellationToken cancellationToken)
    {
        if (infrastructure != null)
        {
            await infrastructure.Shutdown(cancellationToken);
        }

        transportDefinition = null;
    }
}