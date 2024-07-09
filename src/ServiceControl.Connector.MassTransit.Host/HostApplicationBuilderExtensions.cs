using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

static class HostApplicationBuilderExtensions
{
    public static void UseMassTransitConnector(this HostApplicationBuilder builder)
    {
        var setupInfrastructure = Environment.GetCommandLineArgs().Contains("--setup");
        var returnQueue = builder.Configuration.GetValue<string?>("ReturnQueue") ?? "FailWhenReceivingMyMessage_adapter";
        var errorQueue = builder.Configuration.GetValue<string?>("ErrorQueue") ?? "error";

        builder.Services.AddSingleton<Configuration>(new Configuration
            {
                ReturnQueue = returnQueue,
                ErrorQueue = errorQueue,
                SetupInfrastructure = setupInfrastructure
            })
            .AddSingleton<Service>()
            .AddSingleton<MassTransitConverter>()
            .AddSingleton<MassTransitFailureAdapter>()
            .AddSingleton<ReceiverFactory>()
            .AddHostedService<Service>(p => p.GetRequiredService<Service>());

        var transporttype = builder.Configuration.GetValue<string>("TRANSPORTTYPE");
        var connectionstring = builder.Configuration.GetValue<string>("CONNECTIONSTRING");

        switch (transporttype)
        {
            case "AmazonSQS":
                builder.Services.UsingAmazonSqs();
                break;
            case "NetStandardAzureServiceBus":
                builder.Services.UsingAzureServiceBus(connectionstring ?? throw new Exception("CONNECTIONSTRING not specified"));
                break;
            case "RabbitMQ.QuorumConventionalRouting":
                var managementApi = builder.Configuration.GetValue<Uri>("MANAGEMENTAPI") ?? throw new Exception("MANAGEMENTAPI not specified");
                builder.Services.UsingRabbitMQ(managementApi);
                break;
            default:
                throw new NotSupportedException($"Transport type {transporttype} is not supported");
        }
    }
}