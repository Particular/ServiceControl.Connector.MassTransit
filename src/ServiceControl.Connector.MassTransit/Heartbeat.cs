namespace ServiceControl.Connector.MassTransit;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;
using ServiceControl.Monitoring;
using System;
using System.Threading;

public class Heartbeat(
    ILogger<Heartbeat> logger,
    TransportDefinitionFactory transportDefinitionFactory,
    IQueueInformationProvider queueInformationProvider,
    IQueueFilter queueFilter,
    TimeProvider timeProvider,
    Configuration configuration
    ) : BackgroundService
{

    internal const string ConnectedApplicationName = "MassTransitConnector";

    async Task<string[]> GetReceiveQueues(CancellationToken cancellationToken)
    {
        try
        {
            var queues = await queueInformationProvider.GetQueues(cancellationToken);
            return queues.Where(queueFilter.IsMatch).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation($"Cancelled queue query request");
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

        if (!configuration.IsRun)
        {
            return;
        }

        var endpointConfiguration = new EndpointConfiguration(configuration.ReturnQueue);
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
        endpointConfiguration.SendOnly();

        var routing = endpointConfiguration.UseTransport(transportDefinitionFactory.CreateTransportDefinition());
        routing.RouteToEndpoint(typeof(ConnectedApplication), configuration.ControlQueue);

        var endpointInstance = await Endpoint.Start(endpointConfiguration, shutdownToken);

        try
        {

            using PeriodicTimer timer = new(configuration.HeartbeatInterval, timeProvider);

            do
            {
                using var scope = logger.BeginScope("HEARTBEAT");
                try
                {
                    var massTransitErrorQueues = await GetReceiveQueues(shutdownToken);

                    await endpointInstance.Send(
                        new ConnectedApplication
                        {
                            Application = ConnectedApplicationName,
                            SupportsHeartbeats = false,
                            ErrorQueues = massTransitErrorQueues,
                        }, shutdownToken);
                }
#pragma warning disable PS0019
                catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore PS0019
                {
                    logger.LogError(ex, "Failed to send heartbeat");
                }
            } while (await timer.WaitForNextTickAsync(shutdownToken));
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
            logger.LogInformation($"Stopping {nameof(Heartbeat)}");
        }
        finally
        {
            await endpointInstance.Stop(CancellationToken.None);
        }
    }
}
