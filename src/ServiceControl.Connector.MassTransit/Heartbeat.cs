using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;
using ServiceControl.Connector.MassTransit;

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
                    var massTransitConnectorHeartbeat = new MassTransitConnectorHeartbeat
                    {
                        SentDateTimeOffset = DateTimeOffset.UtcNow,
                        Version = ConnectorVersion.Version,
                        Logs = [.. diagnosticsData.RecentLogEntries],
                        ErrorQueues = diagnosticsData.MassTransitErrorQueues.Select(name => new ErrorQueue
                        {
                            Name = name,
                            Ingesting = !diagnosticsData.MassTransitNotFoundQueues.Contains(name)
                        }).ToArray()
                    };

                    await endpointInstance.Send(
                        new HeartbeatMessageSizeReducer(massTransitConnectorHeartbeat).Reduce(), cancellationToken);
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

    public class HeartbeatMessageSizeReducer(MassTransitConnectorHeartbeat originalHeartbeat)
    {
        // Ensuring message is less than 256 KBytes. This is the default in AmazonSQS, and it seems Azure ServiceBus and Rabbit support higher values.
        const long MaxMessageSizeInByte = 200 * 1000;

        public MassTransitConnectorHeartbeat Reduce()
        {
            if (IsValid(originalHeartbeat))
            {
                return originalHeartbeat;
            }

            // First we reduce the logs
            var heartbeat = originalHeartbeat;
            do
            {
                heartbeat = new MassTransitConnectorHeartbeat
                {
                    SentDateTimeOffset = heartbeat.SentDateTimeOffset,
                    Version = heartbeat.Version,
                    ErrorQueues = heartbeat.ErrorQueues,
                    Logs = heartbeat.Logs.Take(heartbeat.Logs.Length - 10).ToArray()
                };

                if (IsValid(heartbeat))
                {
                    return heartbeat;
                }

            } while (heartbeat.Logs.Length > 0);

            // Second we reduce the error queues
            do
            {
                heartbeat = new MassTransitConnectorHeartbeat
                {
                    SentDateTimeOffset = heartbeat.SentDateTimeOffset,
                    Version = heartbeat.Version,
                    ErrorQueues = heartbeat.ErrorQueues.Take(heartbeat.ErrorQueues.Length - 10).ToArray(),
                    Logs = heartbeat.Logs
                };

                if (IsValid(heartbeat))
                {
                    return heartbeat;
                }
            } while (heartbeat.ErrorQueues.Length > 0);

            return heartbeat;
        }

        static bool IsValid(MassTransitConnectorHeartbeat heartbeat)
        {
            var content = JsonSerializer.Serialize(heartbeat);
            return Encoding.UTF8.GetBytes(content).LongLength <= MaxMessageSizeInByte;
        }
    }
}