using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Connector.MassTransit;

static class HostApplicationBuilderExtensions
{
    public static void UseMassTransitConnector(this HostApplicationBuilder builder, bool isSetupOnly)
    {
        var returnQueue = builder.Configuration.GetValue<string?>("RETURN_QUEUE") ??
                          "Particular.ServiceControl.Connector.MassTransit_return";
        var errorQueue = builder.Configuration.GetValue<string?>("ERROR_QUEUE") ?? "error";
        var customChecksQueue = builder.Configuration.GetValue<string?>("SERVICECONTROL_QUEUE") ?? "Particular.ServiceControl";
        var services = builder.Services;

        services
            .AddSingleton(new Configuration
            {
                ReturnQueue = returnQueue,
                ErrorQueue = errorQueue,
                ServiceControlQueue = customChecksQueue
            })
            .AddSingleton<MassTransitConverter>()
            .AddSingleton<MassTransitFailureAdapter>()
            .AddSingleton<ReceiverFactory>()
            .AddSingleton<IProvisionQueues, ProvisionQueues>()
            .AddSingleton(TimeProvider.System);

        var configuration = builder.Configuration;
        var staticQueueList = string.Empty;

        if (!isSetupOnly)
        {
            staticQueueList = configuration.GetValue<string>("QUEUES_FILE");

            if (staticQueueList == null)
            {
                throw new Exception("QUEUES_FILE environment variable not set. Please set this to the path of a file containing a list of queues to bridge. You can use the `queues-list` cli command to populate this file.");
            }

            if (!File.Exists(staticQueueList))
            {
                throw new Exception($"Queues file ({staticQueueList}) specified does not exist.");
            }

            var content = File.ReadAllText(staticQueueList);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception($"Queues file ({staticQueueList}) specified is empty. In order for the connector to bridge error queues, you need to specify some queues! You can use the `queues-list` cli command to populate this file.");
            }

            services
                .AddHostedService<Service>()
                .AddHostedService<Heartbeat>()
                .AddHostedService<CustomCheckReporter>();
        }

        var transportType = configuration.GetValue<string>("TRANSPORT_TYPE");
        var connectionString = configuration.GetValue<string>("CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            try
            {
                new DbConnectionStringBuilder { ConnectionString = connectionString };
            }
            catch (Exception)
            {
                throw new Exception("CONNECTION_STRING environment variable contains an invalid connection string. Please check the value and try again.");
            }
        }

        switch (transportType)
        {
            case "AmazonSQS":
                services.UsingAmazonSqs();
                break;
            case "AzureServiceBus":
                services.UsingAzureServiceBus(configuration,
                    connectionString ?? throw new Exception("CONNECTION_STRING environment variable not set."));
                break;
            case "AzureServiceBusWithDeadLetter":
                services.UsingAzureServiceBus(configuration,
                    connectionString ?? throw new Exception("CONNECTION_STRING environment variable not set."), true);
                break;
            case "RabbitMQ":
                var managementApiValue = configuration.GetValue<string>("RABBITMQ_MANAGEMENT_API_URL") ?? throw new Exception("RABBITMQ_MANAGEMENT_API_URL environment variable not set.");
                if (!Uri.TryCreate(managementApiValue, UriKind.Absolute, out var managementApi))
                {
                    throw new Exception("RABBITMQ_MANAGEMENT_API_URL is invalid. Ensure the value is a valid url without any quotes i.e. http://localhost:15672.");
                }
                services.UsingRabbitMQ(connectionString ?? throw new Exception("CONNECTION_STRING environment variable not set."), managementApi, configuration.GetValue<string>("RABBITMQ_MANAGEMENT_API_USERNAME"), configuration.GetValue<string>("RABBITMQ_MANAGEMENT_API_PASSWORD"));
                break;
            default:
                throw new NotSupportedException($"TRANSPORT_TYPE environment variable specified has an invalid value ({transportType}). Please use one of the following: AmazonSQS, AzureServiceBus, AzureServiceBusWithDeadLetter, RabbitMQ.");
        }

        services.AddSingleton<IFileBasedQueueInformationProvider>(new FileBasedQueueInformationProvider(staticQueueList));
    }
}