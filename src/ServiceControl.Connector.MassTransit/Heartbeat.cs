using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;
using ServiceControl.Adapter.MassTransit;
using ServiceControl.Monitoring;

namespace ServiceControl.Adapter.MassTransit
{
    public class Heartbeat(
        ILogger<Service> logger,
        TransportDefinitionFactory transportDefinitionFactory,
        IQueueInformationProvider queueInformationProvider,
        IQueueFilter queueFilter,
        Configuration configuration
        ) : BackgroundService
    {

        internal const string ConnectedApplicationName = "MassTransitConnector";

#pragma warning disable PS0018
        async Task<string[]> GetReceiveQueues()
#pragma warning restore PS0018
        {
            try
            {
                var queues = await queueInformationProvider.GetQueues();
                return queues.Where(queueFilter.IsMatch).ToArray();
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
            try
            {
                var endpointConfiguration = new EndpointConfiguration(configuration.ReturnQueue);
                var serializer = endpointConfiguration.UseSerialization<SystemJsonSerializer>();
                endpointConfiguration.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
                endpointConfiguration.SendOnly();

                var routing = endpointConfiguration.UseTransport(transportDefinitionFactory.CreateTransportDefinition());
                routing.RouteToEndpoint(typeof(ConnectedApplication), configuration.ControlQueue);

                var endpointInstance = await Endpoint.Start(endpointConfiguration);

                while (!shutdownToken.IsCancellationRequested)
                {
                    var massTransitErrorQueues = await GetReceiveQueues();

                    using var scope = logger.BeginScope("HEARTBEAT");
                    await endpointInstance.Send(new ConnectedApplication
                    {
                        Application = ConnectedApplicationName,
                        SupportsHeartbeats = false,
                        ErrorQueues = massTransitErrorQueues,
                    });

                    await Task.Delay(configuration.HeartbeatInterval, shutdownToken);
                }

                await endpointInstance.Stop();
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // Ignore
            }

            await StopAsync(shutdownToken);
        }
    }
}
