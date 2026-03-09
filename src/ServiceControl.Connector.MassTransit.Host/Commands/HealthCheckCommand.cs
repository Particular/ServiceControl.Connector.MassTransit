namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class HealthCheckCommand : Command
{
    public HealthCheckCommand() : base("health-check", "Performs a validation that the connector is able to connect to the broker")
    {
        this.AddConnectorOptions();

        this.SetHandler(async context =>
        {
            var connectorArgs = ConnectorCommandOptions.BuildArgs(context.ParseResult);

            context.ExitCode = await InternalHandler(connectorArgs, context.GetCancellationToken());
        });
    }

    async Task<int> InternalHandler(string[] connectorArgs, CancellationToken cancellationToken)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddCommandLine(connectorArgs);
        builder.UseMassTransitConnector(true);

        var host = builder.Build();

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