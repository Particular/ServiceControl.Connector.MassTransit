using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public class Service(
  ILogger<Service> logger,
  TransportDefinition transportDefinition,
  IQueueInformationProvider queueInformationProvider,
  MassTransitFailureAdapter adapter,
  Configuration configuration,
  ReceiverFactory receiverFactory
  ) : BackgroundService
{
  TransportInfrastructure? infrastructure;
  HashSet<string>? massTransitErrorQueues;

  async Task<HashSet<string>> GetErrorAndSkippedQueues()
  {
    var queues = await queueInformationProvider.GetQueues();
    return queues.Where(x => x.EndsWith("_error") || x.EndsWith("_skipped")).ToHashSet();
  }

  protected override async Task ExecuteAsync(CancellationToken shutdownToken)
  {
    massTransitErrorQueues = await GetErrorAndSkippedQueues();
    await Setup(shutdownToken);

    try
    {
      while (!shutdownToken.IsCancellationRequested)
      {
        await Task.Delay(TimeSpan.FromMinutes(1), shutdownToken);

        logger.LogDebug("Check for changes in *_error and *_skipped queues on broker");
        var newData = await GetErrorAndSkippedQueues();

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
      criticalErrorAction: (_, exception, _) => { logger.LogCritical(exception, "Critical error"); },
      setupInfrastructure: configuration.SetupInfrastructure
    );

    var receiveSettings = new List<ReceiveSettings>
    {
      new (
        id: "Return",
        receiveAddress: new QueueAddress(configuration.ReturnQueue),
        usePublishSubscribe: false,
        purgeOnStartup: false,
        errorQueue: configuration.ReturnQueue + ".error"
      )
    };

    if (!configuration.SetupInfrastructure)
    {
      foreach (var massTransitErrorQueue in massTransitErrorQueues!)
      {
        logger.LogInformation("listening to: {InputQueue}", massTransitErrorQueue);
        receiveSettings.Add(receiverFactory.Create(massTransitErrorQueue));
      }

      if (!receiveSettings.Any())
      {
        throw new InvalidOperationException("No input queues discovered");
      }
    }

    var receiverSettings = receiveSettings.ToArray();

    infrastructure = await transportDefinition.Initialize(hostSettings, receiverSettings, [], cancellationToken);

    var messageDispatcher = infrastructure.Dispatcher;

    OnMessage forwardMessage =(context, token) =>
    {
      var operation = adapter.ForwardMassTransitErrorToServiceControl(context, messageDispatcher, token);
      return messageDispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), token);
    };

    OnMessage returnMessage = (context, token) =>
    {
      var operation =  adapter.ForwardMassTransitErrorToServiceControl(context, messageDispatcher, token);
      return messageDispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), token);
    };

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
          logger.LogError(context.Exception, "Discarding due to failure: {ExceptionMessage}", context.Exception.Message);
          return Task.FromResult(ErrorHandleResult.Handled);
        }, cancellationToken: cancellationToken);

      await receiver.StartReceive(cancellationToken);
    }
  }

  async Task Teardown(CancellationToken cancellationToken)
  {
    await infrastructure!.Shutdown(cancellationToken);
  }
}