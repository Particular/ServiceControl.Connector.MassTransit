using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit;

public class Service(
    ILogger<Service> logger,
    TransportInfrastructureFactory transportInfrastructureFactory,
    IQueueInformationProvider queueInformationProvider,
    IQueueFilter queueFilter,
    IUserProvidedQueueNameFilter userQueueNameFilter,
    MassTransitFailureAdapter adapter,
    Configuration configuration,
    ReceiverFactory receiverFactory,
    IHostApplicationLifetime hostApplicationLifetime,
    TimeProvider timeProvider
) : BackgroundService
{
    TransportInfrastructure? infrastructure;
    HashSet<string> massTransitErrorQueues = [];

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Cancelled queue query request");
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failure querying the queue information");
        }

        return [];
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(Service)}");

        try
        {
            using PeriodicTimer timer = new(configuration.QueueScanInterval, timeProvider);
            do
            {
                try
                {
                    var newData = await GetReceiveQueues(cancellationToken);
                    var errorQueuesAreTheSame = newData.SetEquals(massTransitErrorQueues);

                    if (errorQueuesAreTheSame)
                    {
                        logger.LogInformation("No changes detected for Masstransit queues");
                        continue;
                    }

                    massTransitErrorQueues = newData;

                    if (infrastructure is not null)
                    {
                        logger.LogInformation("Changes detected, restarting");

                        await StopReceiving(CancellationToken.None);
                    }

                    await StartReceiving(CancellationToken.None);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Failed to shoveling Masstransit messages");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation($"Stopping {nameof(Service)}");

            try
            {
                await StopReceiving(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop all the receivers");
            }
        }
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

        foreach (var massTransitErrorQueue in massTransitErrorQueues)
        {
            logger.LogInformation("Ingesting messages from {InputQueue}", massTransitErrorQueue);
            receiveSettings.Add(receiverFactory.Create(massTransitErrorQueue, configuration.ErrorQueue));
        }

        if (!receiveSettings.Any())
        {
            throw new InvalidOperationException("No input queues discovered");
        }

        var receiverSettings = receiveSettings.ToArray();

        infrastructure = await transportInfrastructureFactory.CreateTransportInfrastructure(hostSettings, receiverSettings, [], cancellationToken);

        var messageDispatcher = infrastructure.Dispatcher;

        OnMessage forwardMessage = async (messageContext, token) =>
        {
            logger.LogInformation("FORWARD {ReceiveAddress} {NativeMessageId}", messageContext.ReceiveAddress, messageContext.NativeMessageId);
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
            logger.LogInformation("RETURN {ReceiveAddress} {NativeMessageId}", messageContext.ReceiveAddress, messageContext.NativeMessageId);
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
            var onMessage = receiverSetting.Id == "Return"
                ? returnMessage
                : forwardMessage;

            var receiver = infrastructure.Receivers[receiverSetting.Id];
            await receiver.Initialize(PushRuntimeSettings.Default,
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
                        var address = new UnicastAddressTag(configuration.PoisonQueue);
                        var operation = new TransportOperation(poisonMessage, address);
                        var operations = new TransportOperations(operation);

                        await messageDispatcher.Dispatch(operations, context.TransportTransaction, token);
                        return ErrorHandleResult.Handled;
                    }

                    // Exponential back-off with jitter
                    var millisecondsDelay = (int)Math.Min(30000, 100 * Math.Pow(2, context.ImmediateProcessingFailures));
                    millisecondsDelay += Random.Shared.Next(millisecondsDelay / 5);

                    logger.LogWarning(context.Exception, "Retrying message {QueueName}, attempt {ImmediateProcessingFailures} of {MaxRetries} after {millisecondsDelay} milliseconds", configuration.ErrorQueue, context.ImmediateProcessingFailures, configuration.MaxRetries, millisecondsDelay);
                    await Task.Delay(millisecondsDelay, token);
                    return ErrorHandleResult.RetryRequired;
                }, cancellationToken);

            await receiver.StartReceive(cancellationToken);
        }
    }

    async Task StopReceiving(CancellationToken cancellationToken)
    {
        if (infrastructure != null)
        {
            var tasks = infrastructure.Receivers.Select(pair => pair.Value.StopReceive(cancellationToken));
            await Task.WhenAll(tasks);
            await infrastructure.Shutdown(cancellationToken);
        }
    }
}