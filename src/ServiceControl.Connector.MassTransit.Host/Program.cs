using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using ServiceControl.Connector.MassTransit.Host.Commands;

var startupCommand = new StartupCommand(args);

startupCommand.AddCommand(new QueuesCommand());
startupCommand.AddCommand(new HealthCheckCommand());

var commandLineBuilder = new CommandLineBuilder(startupCommand);

commandLineBuilder
    .UseVersionOption()
    .UseHelp()
    .UseTypoCorrections()
    .UseParseErrorReporting()
    .UseExceptionHandler()
    .CancelOnProcessTermination();

var parser = commandLineBuilder.Build();
return await parser.InvokeAsync(args);
