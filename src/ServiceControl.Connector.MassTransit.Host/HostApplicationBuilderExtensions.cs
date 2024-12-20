using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Connector.MassTransit;

static class HostApplicationBuilderExtensions
{
    public static void UseMassTransitConnector(this HostApplicationBuilder builder)
    {
        var commandLineArgs = Environment.GetCommandLineArgs();
        var command = commandLineArgs.Contains("--setup")
                ? Command.Setup
                : commandLineArgs.Contains("--setup-and-run") ? Command.SetupAndRun : Command.Run;

        var returnQueue = builder.Configuration.GetValue<string?>("ReturnQueue") ??
                          "Particular.ServiceControl.Connector.MassTransit_return";
        var errorQueue = builder.Configuration.GetValue<string?>("ErrorQueue") ?? "error";

        var queueFilter = builder.Configuration.GetValue<string?>("QUEUENAMEREGEXFILTER");

        var services = builder.Services;

        services.AddSingleton(new Configuration
        {
            ReturnQueue = returnQueue,
            ErrorQueue = errorQueue,
            Command = command
        })
            .AddSingleton<IUserProvidedQueueNameFilter>(new UserProvidedQueueNameFilter(queueFilter))
            .AddSingleton<IQueueFilter, ErrorAndSkippedQueueFilter>()
            .AddSingleton<MassTransitConverter>()
            .AddSingleton<MassTransitFailureAdapter>()
            .AddSingleton<ReceiverFactory>()
            .AddSingleton<IProvisionQueues, ProvisionQueues>()
            .AddSingleton(TimeProvider.System);

        if (command != Command.Setup)
        {
            services.AddHostedService<Service>();
        }

        var configuration = builder.Configuration;

        var transporttype = configuration.GetValue<string>("TRANSPORTTYPE");
        var connectionstring = configuration.GetValue<string>("CONNECTIONSTRING");

        if (string.IsNullOrEmpty(connectionstring))
        {
            try
            {
                new DbConnectionStringBuilder { ConnectionString = connectionstring };
            }
            catch (Exception)
            {
                throw new Exception("CONNECTIONSTRING contains invalid value");
            }
        }

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
                var managementApiValue = configuration.GetValue<string>("MANAGEMENT_API") ?? throw new Exception("MANAGEMENT_API not specified");
                if (!Uri.TryCreate(managementApiValue, UriKind.Absolute, out var managementApi))
                {
                    throw new Exception("MANAGEMENTAPI is invalid. Ensure the value is a valid url without any quotes i.e. http://localhost:15672");
                }
                if (string.IsNullOrEmpty(configuration.GetValue<string>("MANAGEMENT_API_USERNAME")))
                {
                    throw new Exception("MANAGEMENT_API_USERNAME must contain username and password i.e. http://guest:guest@localhost:15672");
                }
                services.UsingRabbitMQ(connectionstring ?? throw new Exception("CONNECTIONSTRING not specified"), managementApi, configuration.GetValue<string>("MANAGEMENT_API_USERNAME"), configuration.GetValue<string>("MANAGEMENT_API_PASSWORD"));
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