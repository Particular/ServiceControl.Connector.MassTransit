using System.Text;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("\x1b[1;37;5mTServiceControl.Connector.MassTransit.Host\x1b[0m");

var builder = Host.CreateApplicationBuilder(args);
builder.UseMassTransitConnector();

using var host = builder.Build();

await host.RunAsync();