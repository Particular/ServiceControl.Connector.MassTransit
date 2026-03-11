namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class HealthCheckCommand : Command
{
    public HealthCheckCommand() : base("health-check", "Performs a validation that the connector is able to connect to the broker")
    {
        this.AddConnectorOptions();

        SetAction(async (parseResult, cancellationToken) =>
        {
            var connectorArgs = ConnectorCommandOptions.BuildArgs(parseResult);

            return await InternalHandler(connectorArgs, cancellationToken);
        });
    }

    async Task<int> InternalHandler(string[] connectorArgs, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder(connectorArgs);
        builder.UseMassTransitConnector(true);

        using var host = builder.Build();

        var queueInformationProvider = host.Services.GetRequiredService<IHealthCheckerProvider>();
        var (success, errorMessage) = await queueInformationProvider.TryCheck(cancellationToken);

        if (!success)
        {
            Console.WriteLine(errorMessage);
            return 1;
        }

        Console.WriteLine("Success");

        return 0;
    }
}