namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Extensions.Logging;
using ServiceControl.Connector.MassTransit;

public class StartupCommand : RootCommand
{
    public StartupCommand(string[] args) : base("Particular Software ServiceControl Masstransit Connector")
    {
        var consoleOption = new Option<bool>(
            "--console",
            "Run in console mode.");
        consoleOption.AddAlias("-c");

        var runModeOption = new Option<RunMode>(
            "--run-mode",
            () => RunMode.SetupAndRun,
            "Mode to run in.")
        { Arity = ArgumentArity.ExactlyOne };

        AddOption(consoleOption);
        AddOption(runModeOption);

        this.SetHandler(async context =>
        {
            var isConsole = context.ParseResult.GetValueForOption(consoleOption);
            var runMode = context.ParseResult.GetValueForOption(runModeOption);

            context.ExitCode = await InternalHandler(runMode, isConsole, args, context.GetCancellationToken());
        });
    }

    async Task<int> InternalHandler(RunMode runMode, bool isConsole, string[] args, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.UseMassTransitConnector(runMode == RunMode.Setup);

        if (isConsole)
        {
            builder.Logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.IncludeScopes = true;
            });
        }
        else
        {
            builder.Logging.AddSystemdConsole();
        }

        using var host = builder.Build();

        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        NServiceBus.Logging.LogManager.UseFactory(new ExtensionsLoggerFactory(loggerFactory));

        RecordStartup(host.Services.GetRequiredService<Configuration>(), loggerFactory);

        var provisionQueues = host.Services.GetRequiredService<IProvisionQueues>();
        var provisionQueuesResult = true;

        if (runMode != RunMode.Run)
        {
            provisionQueuesResult = await provisionQueues.TryProvision(cancellationToken);
        }

        if (!provisionQueuesResult)
        {
            return 1;
        }

        if (runMode != RunMode.Setup)
        {
            await host.RunAsync(cancellationToken);
        }

        return 0;
    }

    static void RecordStartup(Configuration settings, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Diagnostics");
        logger.LogInformation("-------------------------------------------------------------------------------------------");
        logger.LogInformation("Connector Version:                  {Version}", ConnectorVersion.Version);
        logger.LogInformation("Error Queue:                        {ErrorQueue}", settings.ErrorQueue});
        logger.LogInformation("Service Control Queue:              {ServiceControlQueue}", settings.ServiceControlQueue);
        logger.LogInformation("Poison Queue:                       {PoisonQueue}", settings.PoisonQueue);
        logger.LogInformation("Return Queue:                       {ReturnQueue}", settings.ReturnQueue);
        logger.LogInformation("Queue Scan Interval:                {QueueScanInterval}", settings.QueueScanInterval);
        logger.LogInformation("Custom Checks Interval:             {CustomChecksInterval}", settings.CustomChecksInterval);
        logger.LogInformation("Heartbeat Interval:                 {HeartbeatInterval}", settings.HeartbeatInterval);
        logger.LogInformation("-------------------------------------------------------------------------------------------");
    }
}