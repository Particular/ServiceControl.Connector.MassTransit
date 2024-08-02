using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

static class HostApplicationBuilderExtensions
{
    public static void UseMassTransitConnector(this HostApplicationBuilder builder)
    {
        var setupInfrastructure = Environment.GetCommandLineArgs().Contains("--setup");
        var returnQueue = builder.Configuration.GetValue<string?>("ReturnQueue") ??
                          "FailWhenReceivingMyMessage_adapter";
        var errorQueue = builder.Configuration.GetValue<string?>("ErrorQueue") ?? "error";

        var services = builder.Services;

        services.AddSingleton<Configuration>(new Configuration
        {
            ReturnQueue = returnQueue,
            ErrorQueue = errorQueue,
            SetupInfrastructure = setupInfrastructure
        })
        .AddSingleton<IQueueFilter, ErrorAndSkippedQueueFilter>()
        .AddSingleton<Service>()
        .AddSingleton<MassTransitConverter>()
        .AddSingleton<MassTransitFailureAdapter>()
        .AddSingleton<ReceiverFactory>()
        .AddHostedService<Service>(p => p.GetRequiredService<Service>());

        var configuration = builder.Configuration;

        var transporttype = configuration.GetValue<string>("TRANSPORTTYPE");
        var connectionstring = configuration.GetValue<string>("CONNECTIONSTRING");

        switch (transporttype)
        {
            case "AmazonSQS":
                services.UsingAmazonSqs();
                break;
            case "NetStandardAzureServiceBus":
                services.UsingAzureServiceBus(configuration,
                    connectionstring ?? throw new Exception("CONNECTIONSTRING not specified"));
                break;
            case "RabbitMQ.QuorumConventionalRouting":
                var managementApi = configuration.GetValue<Uri>("MANAGEMENTAPI") ?? throw new Exception("MANAGEMENTAPI not specified");
                services.UsingRabbitMQ(connectionstring ?? throw new Exception("CONNECTIONSTRING not specified"), managementApi);
                break;
            default:
                throw new NotSupportedException($"Transport type {transporttype} is not supported");
        }

        // Override any transport specific implementation
        var staticQueueList = configuration.GetValue<string?>("QUEUES_FILE");

        if (staticQueueList != null)
        {
            services.AddSingleton<IQueueInformationProvider>(new FileBasedQueueInformationProvider(staticQueueList));
        }
    }
}