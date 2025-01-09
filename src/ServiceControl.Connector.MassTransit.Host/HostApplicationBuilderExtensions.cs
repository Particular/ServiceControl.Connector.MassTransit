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

        var returnQueue = builder.Configuration.GetValue<string?>("RETURN_QUEUE") ??
                          "Particular.ServiceControl.Connector.MassTransit_return";
        var errorQueue = builder.Configuration.GetValue<string?>("ERROR_QUEUE") ?? "error";

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

        var transporttype = configuration.GetValue<string>("TRANSPORT_TYPE");
        var connectionstring = configuration.GetValue<string>("CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionstring))
        {
            try
            {
                new DbConnectionStringBuilder { ConnectionString = connectionstring };
            }
            catch (Exception)
            {
                throw new Exception("CONNECTION_STRING contains invalid value");
            }
        }

        switch (transporttype)
        {
            case "AmazonSQS":
                services.UsingAmazonSqs();
                break;
            case "AzureServiceBus":
                services.UsingAzureServiceBus(configuration,
                    connectionstring ?? throw new Exception("CONNECTION_STRING not specified"));
                break;
            case "AzureServiceBusWithDeadLetter":
                services.UsingAzureServiceBus(configuration,
                    connectionstring ?? throw new Exception("CONNECTION_STRING not specified"), true);
                break;
            case "RabbitMQ":
                var managementApiValue = configuration.GetValue<string>("RABBITMQ_MANAGEMENT_API_URL") ?? throw new Exception("RABBITMQ_MANAGEMENT_API_URL not specified");
                if (!Uri.TryCreate(managementApiValue, UriKind.Absolute, out var managementApi))
                {
                    throw new Exception("RABBITMQ_MANAGEMENT_API_URL is invalid. Ensure the value is a valid url without any quotes i.e. http://localhost:15672");
                }
                services.UsingRabbitMQ(connectionstring ?? throw new Exception("CONNECTION_STRING not specified"), managementApi, configuration.GetValue<string>("RABBITMQ_MANAGEMENT_API_USERNAME"), configuration.GetValue<string>("RABBITMQ_MANAGEMENT_API_PASSWORD"));
                break;
            default:
                throw new NotSupportedException($"Transport type {transporttype} is not supported");
        }

        services.AddSingleton<IFileBasedQueueInformationProvider>(new FileBasedQueueInformationProvider(staticQueueList));
    }
}