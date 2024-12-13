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
    IHostApplicationLifetime hostApplicationLifetime
) : IHostedService
{
    readonly CancellationTokenSource StopCancellationTokenSource = new();
    Task? loopTask;

    TransportInfrastructure? infrastructure;
    HashSet<string>? massTransitErrorQueues;

#pragma warning disable PS0018
    async Task<HashSet<string>> GetReceiveQueues()
#pragma warning restore PS0018
    {
        try
        {
            var queues = await queueInformationProvider.GetQueues();
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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly()!.Location).ProductVersion!;
        logger.LogInformation("ServiceControl.Connector.MassTransit {Version}", version);

        //Perform setup
        if (configuration.IsSetup)
        {
            massTransitErrorQueues = await GetReceiveQueues();

            await Setup(cancellationToken);

            if (!configuration.IsRun)
            {
                logger.LogInformation("Signaling stop as only run in setup mode");
                hostApplicationLifetime.StopApplication();
                return;
            }
        }

        massTransitErrorQueues = [];
        loopTask = Loop(StopCancellationTokenSource.Token);
    }

    async Task Loop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var newData = await GetReceiveQueues();

                var errorQueuesAreNotTheSame = !newData.SetEquals(massTransitErrorQueues!);

                if (errorQueuesAreNotTheSame)
                {
                    logger.LogInformation("Changes detected, restarting");
                    await StopReceiving(cancellationToken);
                    massTransitErrorQueues = newData;
                    await StartReceiving(cancellationToken);
                }

                await Task.Delay(configuration.QueueScanInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Shutting down initiated by host");
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Failure");
            hostApplicationLifetime.StopApplication();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await StopCancellationTokenSource.CancelAsync();
        if (loopTask != null)
        {
            await loopTask;
        }
        await StopReceiving(cancellationToken);
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

        infrastructure = await transportInfrastructureFactory.CreateTransportInfrastructure(
             hostSettings,
             receiverSettings,
             [configuration.PoisonQueue, configuration.CustomChecksQueue],
             cancellationToken
         );
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

        infrastructure = await transportInfrastructureFactory.CreateTransportInfrastructure(
            hostSettings,
            receiverSettings,
            [configuration.PoisonQueue, configuration.CustomChecksQueue],
            cancellationToken
        );

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
                        logger.LogError(context.Exception, "Moved message to {QueueName}", configuration.PoisonQueue);

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
        var i = infrastructure;
        if (i == null)
        {
            return;
        }
        infrastructure = null;

        // Behavior copied from https://github.com/Particular/NServiceBus/blob/9.2.3/src/NServiceBus.Core/Receiving/ReceiveComponent.cs#L229-L246
        var receivers = i.Receivers.Values;
        var receiverStopTasks = receivers.Select(async receiver =>
        {
            try
            {
                logger.LogDebug("Stopping {ReceiverId} receiver", receiver.Id);
                await receiver.StopReceive(cancellationToken);
                logger.LogDebug("Stopped {ReceiverId} receiver", receiver.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Host is terminating
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Receiver {ReceiverId} threw an exception on stopping.", receiver.Id);
            }
        });

        await Task.WhenAll(receiverStopTasks);
        await i.Shutdown(cancellationToken);
    }
}