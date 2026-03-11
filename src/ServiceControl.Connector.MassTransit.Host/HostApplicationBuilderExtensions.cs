using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceControl.Connector.MassTransit;
using ServiceControl.Connector.MassTransit.Host;

static class HostApplicationBuilderExtensions
{
    public static void UseMassTransitConnector(this HostApplicationBuilder builder, bool isSetupOnly)
    {
        // Bind to root configuration to match command line args format (e.g., --ConnectionString=value)
        builder.Services.AddOptions<ConnectorOptions>()
            .BindConfiguration(string.Empty)
            .PostConfigure(options => ApplyEnvironmentVariableFallbacks(options, builder.Configuration))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<ConnectorOptions>, ConnectorOptionsValidator>();

        // Read options at composition time for eager validation and transport setup
        var options = ReadOptions(builder.Configuration);

        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            try
            {
                new DbConnectionStringBuilder { ConnectionString = options.ConnectionString };
            }
            catch (Exception)
            {
                throw new Exception("CONNECTION_STRING contains an invalid connection string. Please check the value and try again");
            }
        }

        var services = builder.Services;

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            return new Configuration
            {
                ReturnQueue = opts.ReturnQueue,
                ErrorQueue = opts.ErrorQueue,
                ServiceControlQueue = opts.ServiceControlQueue
            };
        });

        services
            .AddSingleton<MassTransitConverter>()
            .AddSingleton<MassTransitFailureAdapter>()
            .AddSingleton<ReceiverFactory>()
            .AddSingleton<IProvisionQueues, ProvisionQueues>()
            .AddSingleton(TimeProvider.System);

        services.AddSingleton<IFileBasedQueueInformationProvider>(
            new FileBasedQueueInformationProvider(isSetupOnly ? string.Empty : options.QueuesFile));

        if (!isSetupOnly)
        {
            if (string.IsNullOrEmpty(options.QueuesFile))
            {
                throw new Exception("QUEUES_FILE is not set. Please use --queues-file or set the QUEUES_FILE environment variable.");
            }

            if (!File.Exists(options.QueuesFile))
            {
                throw new Exception($"Queues file ({options.QueuesFile}) specified does not exist");
            }

            var content = File.ReadAllText(options.QueuesFile);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception($"Queues file ({options.QueuesFile}) specified is empty. In order for the connector to bridge error queues, you need to specify some queues! You can use the `queues-list` cli command to populate this file.");
            }

            services.AddHostedService<Service>();
            services.AddHostedService<Heartbeat>();
            services.AddHostedService<CustomCheckReporter>();

            var diagnosticsData = new DiagnosticsData();
            services.AddSingleton(diagnosticsData);
            builder.Logging.AddProvider(new LastLogEntriesProvider(diagnosticsData));
        }

        ConfigureTransport(services, builder.Configuration, options);
    }

    static void ConfigureTransport(IServiceCollection services, IConfiguration configuration, ConnectorOptions options)
    {
        switch (options.TransportType)
        {
            case "AmazonSQS":
                services.UsingAmazonSqs();
                break;
            case "AzureServiceBus":
                services.UsingAzureServiceBus(configuration, options.ConnectionString, false);
                break;
            case "AzureServiceBusWithDeadLetter":
                services.UsingAzureServiceBus(configuration, options.ConnectionString, true);
                break;
            case "RabbitMQ":
                var managementApiValue = options.RabbitMqManagementApiUrl
                    ?? throw new Exception("RABBITMQ_MANAGEMENT_API_URL is required for RabbitMQ transport. Use --rabbitmq-management-api-url or set the RABBITMQ_MANAGEMENT_API_URL environment variable.");
                if (!Uri.TryCreate(managementApiValue, UriKind.Absolute, out var managementApi))
                {
                    throw new Exception("RABBITMQ_MANAGEMENT_API_URL is invalid. Ensure the value is a valid url without any quotes e.g. http://localhost:15672");
                }
                var queueType = Enum.TryParse(typeof(QueueType), options.RabbitMqQueueType, true, out var queueTypeOut)
                    ? (QueueType)queueTypeOut
                    : QueueType.Quorum;
                services.UsingRabbitMQ(options.ConnectionString, managementApi, options.RabbitMqManagementApiUsername, options.RabbitMqManagementApiPassword, queueType);
                break;
            default:
                throw new NotSupportedException($"TRANSPORT_TYPE specified has an invalid value ({options.TransportType}). Please use one of the following: AzureServiceBus, AzureServiceBusWithDeadLetter, RabbitMQ, AmazonSQS");
        }
    }

    // Reads configuration at composition time, applying CLI-arg-first then env-var fallback.
    // CLI args are mapped to PascalCase keys (e.g. --ConnectionString=value),
    // env vars use SCREAMING_SNAKE_CASE (e.g. CONNECTION_STRING).
    static ConnectorOptions ReadOptions(IConfiguration config) => new()
    {
        ConnectionString = config["ConnectionString"] ?? config["CONNECTION_STRING"] ?? string.Empty,
        TransportType = config["TransportType"] ?? config["TRANSPORT_TYPE"] ?? string.Empty,
        QueuesFile = config["QueuesFile"] ?? config["QUEUES_FILE"] ?? string.Empty,
        ReturnQueue = config["ReturnQueue"] ?? config["RETURN_QUEUE"] ?? "Particular.ServiceControl.Connector.MassTransit_return",
        ErrorQueue = config["ErrorQueue"] ?? config["ERROR_QUEUE"] ?? "error",
        ServiceControlQueue = config["ServiceControlQueue"] ?? config["SERVICECONTROL_QUEUE"] ?? "Particular.ServiceControl",
        RabbitMqManagementApiUrl = config["RabbitMqManagementApiUrl"] ?? config["RABBITMQ_MANAGEMENT_API_URL"],
        RabbitMqManagementApiUsername = config["RabbitMqManagementApiUsername"] ?? config["RABBITMQ_MANAGEMENT_API_USERNAME"],
        RabbitMqManagementApiPassword = config["RabbitMqManagementApiPassword"] ?? config["RABBITMQ_MANAGEMENT_API_PASSWORD"],
        RabbitMqQueueType = config["RabbitMqQueueType"] ?? config["RABBITMQ_QUEUE_TYPE"] ?? "Quorum",
    };

    // Applied as PostConfigure so IOptions<ConnectorOptions> consumers get the same precedence:
    // CLI arg (PascalCase key bound by BindConfiguration) > env var > class default.
    //
    // For required fields (ConnectionString, TransportType, QueuesFile): the class default is
    // empty string, so string.IsNullOrEmpty correctly detects "not set via CLI arg".
    //
    // For optional fields with non-empty class defaults (ReturnQueue, ErrorQueue, etc.):
    // string.IsNullOrEmpty would never fire because BindConfiguration leaves the property at
    // its class default. Instead, check whether a CLI arg key was present in config directly;
    // if not, apply the env var override when one is set.
    static void ApplyEnvironmentVariableFallbacks(ConnectorOptions options, IConfiguration config)
    {
        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            options.ConnectionString = config.GetValue<string>("CONNECTION_STRING") ?? string.Empty;
        }

        if (string.IsNullOrEmpty(options.TransportType))
        {
            options.TransportType = config.GetValue<string>("TRANSPORT_TYPE") ?? string.Empty;
        }

        if (string.IsNullOrEmpty(options.QueuesFile))
        {
            options.QueuesFile = config.GetValue<string>("QUEUES_FILE") ?? string.Empty;
        }

        // For optional settings with non-empty defaults: only apply env var when no CLI arg was supplied.
        if (config["ReturnQueue"] == null)
        {
            options.ReturnQueue = config.GetValue<string>("RETURN_QUEUE") ?? options.ReturnQueue;
        }

        if (config["ErrorQueue"] == null)
        {
            options.ErrorQueue = config.GetValue<string>("ERROR_QUEUE") ?? options.ErrorQueue;
        }

        if (config["ServiceControlQueue"] == null)
        {
            options.ServiceControlQueue = config.GetValue<string>("SERVICECONTROL_QUEUE") ?? options.ServiceControlQueue;
        }

        if (string.IsNullOrEmpty(options.RabbitMqManagementApiUrl))
        {
            options.RabbitMqManagementApiUrl = config.GetValue<string>("RABBITMQ_MANAGEMENT_API_URL");
        }

        if (string.IsNullOrEmpty(options.RabbitMqManagementApiUsername))
        {
            options.RabbitMqManagementApiUsername = config.GetValue<string>("RABBITMQ_MANAGEMENT_API_USERNAME");
        }

        if (string.IsNullOrEmpty(options.RabbitMqManagementApiPassword))
        {
            options.RabbitMqManagementApiPassword = config.GetValue<string>("RABBITMQ_MANAGEMENT_API_PASSWORD");
        }

        if (config["RabbitMqQueueType"] == null)
        {
            options.RabbitMqQueueType = config.GetValue<string>("RABBITMQ_QUEUE_TYPE") ?? options.RabbitMqQueueType;
        }
    }
}