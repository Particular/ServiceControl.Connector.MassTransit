using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Extensions.Logging;
using ServiceControl.Connector.MassTransit;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);
builder.UseMassTransitConnector();

var isConsole = args.Contains("-c") || args.Contains("--console");

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

var provisionQueues = host.Services.GetRequiredService<IProvisionQueues>();
var configure = host.Services.GetRequiredService<Configuration>();
var result = true;

if (configure.Command != Command.Run)
{
    result = await provisionQueues.TryProvision(CancellationToken.None);
}

if (result == false)
{
    return 1;
}

if (configure.Command != Command.Setup)
{
    await host.RunAsync();
}

return 0;
