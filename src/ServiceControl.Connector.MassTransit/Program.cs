using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("\x1b[1;37;5mTransport seam with SQS  send!\x1b[0m");

var builder = Host.CreateApplicationBuilder(args);
builder.Services
  .AddSingleton<Configuration>(new Configuration
  {
    returnQueue = "FailWhenReceivingMyMessage_adapter",
    serviceControlErrorQueue = "error",
    setupInfrastructure = false
  })
  .AddSingleton<Service>()
  .AddSingleton<MassTransitConverter>()
  .AddHostedService<Service>(p => p.GetRequiredService<Service>())
  ;

using var host = builder.Build();

await host.RunAsync();
