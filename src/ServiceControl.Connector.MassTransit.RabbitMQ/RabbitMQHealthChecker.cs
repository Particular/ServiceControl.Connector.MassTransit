using NServiceBus.Transport;
using RabbitMQ.Client.Exceptions;

class RabbitMQHealthChecker(RabbitMQHelper helper, Configuration configuration, TransportInfrastructureFactory transportInfrastructureFactory) : IHealthCheckerProvider
{
    public async Task<(bool, string)> TryCheck(CancellationToken cancellationToken)
    {
        var result = await helper.TryCheck(cancellationToken);

        if (!result.Success)
        {
            return (false, result.ErrorMessage);
        }

        var hostSettings = new HostSettings(
            configuration.ReturnQueue,
            $"Queue creator for {configuration.ReturnQueue}",
            new StartupDiagnosticEntries(),
            (_, __, ___) =>
            {
            },
            false);

        var receiverSettings = new[]{
            new ReceiveSettings(
                id: "Return",
                receiveAddress: new QueueAddress(configuration.ReturnQueue),
                usePublishSubscribe: false,
                purgeOnStartup: false,
                errorQueue: configuration.PoisonQueue)};


        try
        {
            var infrastructure = await transportInfrastructureFactory.CreateTransportInfrastructure(hostSettings,
                receiverSettings, [configuration.PoisonQueue, configuration.ServiceControlQueue], cancellationToken);
            await infrastructure.Shutdown(cancellationToken);
        }
        catch (BrokerUnreachableException e)
        {
            return (false, e.Message);
        }

        return (true, string.Empty);
    }
}