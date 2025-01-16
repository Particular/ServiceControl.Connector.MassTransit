using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public class Heartbeat(
    ILogger<Heartbeat> logger,
    TransportDefinition transportDefinition,
    TimeProvider timeProvider,
    Configuration configuration,
    DiagnosticsData diagnosticsData
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(Heartbeat)}.");

        var endpointConfiguration = new EndpointConfiguration(nameof(Heartbeat));
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
        endpointConfiguration.SendOnly();

        var routing = endpointConfiguration.UseTransport(transportDefinition);
        routing.RouteToEndpoint(typeof(MassTransitConnectorHeartbeat), configuration.ServiceControlQueue);

        var endpointInstance = await Endpoint.Start(endpointConfiguration, cancellationToken);

        try
        {
            using PeriodicTimer timer = new(configuration.HeartbeatInterval, timeProvider);
            do
            {
                try
                {
                    await endpointInstance.Send(
                        new MassTransitConnectorHeartbeat
                        {
                            SentDateTimeOffset = DateTimeOffset.UtcNow,
                            Version = ConnectorVersion.Version,
                            Logs = [.. diagnosticsData.RecentLogEntries],
                            ErrorQueues = diagnosticsData.MassTransitErrorQueues.Select(name => new ErrorQueue
                            {
                                Name = name,
                                Ingesting = !diagnosticsData.MassTransitNotFoundQueues.Contains(name)
                            }).ToArray()
                        }, cancellationToken);
                    logger.LogInformation("Heartbeat sent");
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Failed to send heartbeat");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation($"Stopping {nameof(Heartbeat)}");
        }
        finally
        {
            await endpointInstance.Stop(CancellationToken.None);
        }
    }

    public class MassTransitConnectorHeartbeat : IMessage
    {
        public required string Version { get; init; }
        public required ErrorQueue[] ErrorQueues { get; init; }
        public required string[] Logs { get; init; }
        public required DateTimeOffset SentDateTimeOffset { get; init; }
    }

#pragma warning disable CA1711
    public class ErrorQueue
#pragma warning restore CA1711
    {
        public required string Name { get; init; }
        public required bool Ingesting { get; init; }
    }
}