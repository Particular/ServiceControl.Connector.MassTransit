using ServiceControl.Connector.MassTransit.Host.Commands;

StartupCommand startupCommand = new(args)
{
    new QueuesCommand(),
    new HealthCheckCommand()
};

var exitCode = await startupCommand.Parse(args).InvokeAsync();
return exitCode;
