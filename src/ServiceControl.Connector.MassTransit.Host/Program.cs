using System.CommandLine;
using ServiceControl.Connector.MassTransit.Host.Commands;

var startupCommand = new StartupCommand(args);

startupCommand.AddCommand(new QueuesCommand());

return await startupCommand.InvokeAsync(args);
