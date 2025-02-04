namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class HealthCheckCommand : Command
{
    public HealthCheckCommand() : base("health-check", "Performs a validation that the connector is able to connect to the broker.")
    {
        this.SetHandler(async context =>
        {
            context.ExitCode = await InternalHandler(context.GetCancellationToken());
        });
    }

    async Task<int> InternalHandler(CancellationToken cancellationToken)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddEnvironmentVariables();
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