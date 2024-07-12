using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("\x1b[1;37;5mTServiceControl.Connector.MassTransit.Host\x1b[0m");

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

await host.RunAsync();