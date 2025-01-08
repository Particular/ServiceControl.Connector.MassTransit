namespace ServiceControl.Connector.MassTransit;

using Microsoft.Extensions.Logging;
using NServiceBus.Transport;

public interface IProvisionQueues
{
    Task<bool> TryProvision(CancellationToken cancellationToken);
}

public class ProvisionQueues(Configuration configuration, TransportInfrastructureFactory transportInfrastructureFactory, ILogger<ProvisionQueues> logger) : IProvisionQueues
{
    public async Task<bool> TryProvision(CancellationToken cancellationToken)
    {
        var result = true;

        var hostSettings = new HostSettings(
            configuration.ReturnQueue,
            $"Queue creator for {configuration.ReturnQueue}",
            new StartupDiagnosticEntries(),
            (_, exception, ___) =>
            {
                logger.LogCritical(exception, "Critical error, creating queues.");
                result = false;
            },
            true);

        var receiverSettings = new[]{
            new ReceiveSettings(
                id: "Return",
                receiveAddress: new QueueAddress(configuration.ReturnQueue),
                usePublishSubscribe: false,
                purgeOnStartup: false,
                errorQueue: configuration.PoisonQueue)};

        logger.LogInformation("Creating queues if they don't already exist.");

        var infrastructure = await transportInfrastructureFactory.CreateTransportInfrastructure(hostSettings,
            receiverSettings, [configuration.PoisonQueue], cancellationToken);
        await infrastructure.Shutdown(cancellationToken);

        return result;
    }
}