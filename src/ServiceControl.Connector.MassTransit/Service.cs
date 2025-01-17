using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;

public class Service(
    ILogger<Service> logger,
    TransportInfrastructureFactory transportInfrastructureFactory,
    IQueueInformationProvider queueInformationProvider,
    MassTransitFailureAdapter adapter,
    Configuration configuration,
    ReceiverFactory receiverFactory,
    IHostApplicationLifetime hostApplicationLifetime,
    IFileBasedQueueInformationProvider fileBasedQueueInformationProvider,
    DiagnosticsData diagnosticsData,
    TimeProvider timeProvider
) : BackgroundService
{
    TransportInfrastructure? infrastructure;

    async Task<HashSet<string>> GetReceiveQueues(CancellationToken cancellationToken)
    {
        try
        {
            var queues = fileBasedQueueInformationProvider.GetQueues(cancellationToken);
            var resultList = new HashSet<string>();
            var enumerator = queues.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    if (!string.IsNullOrWhiteSpace(enumerator.Current))
                    {
                        resultList.Add(enumerator.Current);
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            return resultList;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Cancelled queue query request");
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to read the queue names from the file. Returning the previous list of queues.");
        }

        return [.. diagnosticsData.MassTransitErrorQueues];
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(Service)}.");

        try
        {
            using PeriodicTimer timer = new(configuration.QueueScanInterval, timeProvider);
            do
            {
                try
                {
                    var newData = await GetReceiveQueues(cancellationToken);
                    var errorQueuesAreTheSame = newData.SetEquals(diagnosticsData.MassTransitErrorQueues);

                    if (errorQueuesAreTheSame)
                    {
                        logger.LogInformation("No changes detected for MassTransit queues.");

                        if (diagnosticsData.MassTransitNotFoundQueues.Length == 0)
                        {
                            continue;
                        }

                        logger.LogWarning("The following queues were not found: {Queues}. We are going to try to connect to them again.", string.Join(", ", diagnosticsData.MassTransitNotFoundQueues));

                        var foundOne = false;
                        foreach (var notFoundQueue in diagnosticsData.MassTransitNotFoundQueues)
                        {
                            if (await queueInformationProvider.QueueExists(notFoundQueue, cancellationToken))
                            {
                                // If a queue exists, we will restart again.
                                foundOne = true;
                                break;
                            }
                        }

                        if (!foundOne)
                        {
                            continue;
                        }
                    }

                    diagnosticsData.AddErrorQueues(newData);
                    diagnosticsData.ClearNotFound();

                    if (infrastructure is not null)
                    {
                        logger.LogInformation("Changes detected, restarting.");

                        await StopReceiving(CancellationToken.None);
                    }

                    await StartReceiving(CancellationToken.None);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Failed shoveling MassTransit messages.");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation($"Stopping {nameof(Service)}.");

            try
            {
                await StopReceiving(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop all the receivers.");
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
                logger.LogCritical(exception, "Critical error, signaling to stop host.");
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

        foreach (var massTransitErrorQueue in diagnosticsData.MassTransitErrorQueues)
        {
            if (await queueInformationProvider.QueueExists(massTransitErrorQueue, cancellationToken))
            {
                logger.LogInformation("Ingesting messages from {InputQueue}.", massTransitErrorQueue);
                receiveSettings.Add(receiverFactory.Create(massTransitErrorQueue, configuration.ErrorQueue));
            }
            else
            {
                diagnosticsData.AddNotFound(massTransitErrorQueue);
                logger.LogWarning("Queue {InputQueue} does not exist. Remove it from the specified list if no longer required otherwise we are going to try to connect to it again.", massTransitErrorQueue);
            }
        }

        if (!receiveSettings.Any())
        {
            throw new InvalidOperationException("No input queues specified.");
        }

        var receiverSettings = receiveSettings.ToArray();

        infrastructure = await transportInfrastructureFactory.CreateTransportInfrastructure(
            hostSettings,
            receiverSettings,
            [configuration.PoisonQueue, configuration.ServiceControlQueue],
            cancellationToken
        );

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
                throw new ConversionException("Conversion to ServiceControl failed.", e);
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
                throw new ConversionException("Conversion from ServiceControl failed.", e);
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
                        var poisonMessage = new OutgoingMessage(
                            context.Message.MessageId,
                            context.Message.Headers,
                            context.Message.Body
                        );
                        var address = new UnicastAddressTag(configuration.PoisonQueue);
                        var operation = new TransportOperation(poisonMessage, address);
                        var operations = new TransportOperations(operation);

                        await messageDispatcher.Dispatch(operations, context.TransportTransaction, token);

                        logger.LogError(context.Exception, "Moved message to {QueueName}.", configuration.PoisonQueue);

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