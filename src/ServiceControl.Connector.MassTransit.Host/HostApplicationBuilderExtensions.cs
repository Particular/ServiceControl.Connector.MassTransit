using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Transport;
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

        var customChecksQueue = builder.Configuration.GetValue<string?>("CUSTOM_CHECK_QUEUE") ?? "Particular.ServiceControl";
        var services = builder.Services;

        var config = new Configuration
        {
            ReturnQueue = returnQueue,
            ErrorQueue = errorQueue,
            CustomChecksQueue = customChecksQueue,
            Command = command
        };
        services
            .AddSingleton<MassTransitConverter>()
            .AddSingleton<MassTransitFailureAdapter>()
            .AddSingleton<ReceiverFactory>()
            .AddSingleton<IProvisionQueues, ProvisionQueues>()
            .AddSingleton(TimeProvider.System);

        var configuration = builder.Configuration;
        var staticQueueList = string.Empty;

        if (command != Command.Setup)
        {
            staticQueueList = configuration.GetValue<string>("QUEUES_FILE");

            if (staticQueueList == null)
            {
                throw new Exception("QUEUES_FILE not specified");
            }

            services
                .AddHostedService<Service>()
                .AddHostedService<CustomCheckReporter>(provider =>
                    new CustomCheckReporter(
                        provider.GetRequiredService<TransportDefinition>(),
                        provider.GetRequiredService<IQueueLengthProvider>(),
                        config,
                        provider.GetRequiredService<IHostApplicationLifetime>()));
        }

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
                var managementApiValue = configuration.GetValue<string>("MANAGEMENT_API_URL") ?? throw new Exception("MANAGEMENT_API_URL not specified");
                if (!Uri.TryCreate(managementApiValue, UriKind.Absolute, out var managementApi))
                {
                    throw new Exception("MANAGEMENT_API_URL is invalid. Ensure the value is a valid url without any quotes i.e. http://localhost:15672");
                }
                services.UsingRabbitMQ(connectionstring ?? throw new Exception("CONNECTIONSTRING not specified"), managementApi, configuration.GetValue<string>("MANAGEMENT_API_USERNAME"), configuration.GetValue<string>("MANAGEMENT_API_PASSWORD"));
                break;
            default:
                throw new NotSupportedException($"Transport type {transporttype} is not supported");
        }

        services.AddSingleton<IFileBasedQueueInformationProvider>(new FileBasedQueueInformationProvider(staticQueueList));
    }
}