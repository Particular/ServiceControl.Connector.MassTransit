using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public class Heartbeat(
    ILogger<Heartbeat> logger,
    TransportDefinition transportDefinition,
    IFileBasedQueueInformationProvider fileBasedQueueInformationProvider,
    TimeProvider timeProvider,
    Configuration configuration
    ) : BackgroundService
{
    List<string> massTransitErrorQueues = [];

    async Task<List<string>> GetReceiveQueues(CancellationToken cancellationToken)
    {
        try
        {
            var queues = fileBasedQueueInformationProvider.GetQueues(cancellationToken);
            var resultList = new List<string>();
            var enumerator = queues.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    resultList.Add(enumerator.Current);
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

        return massTransitErrorQueues;
    }

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
                    massTransitErrorQueues = await GetReceiveQueues(cancellationToken);

                    await endpointInstance.Send(
                        new MassTransitConnectorHeartbeat
                        {
                            Version = ConnectorVersion.Version,
                            ErrorQueues = [.. massTransitErrorQueues]
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
        public required string[] ErrorQueues { get; init; }
    }
}