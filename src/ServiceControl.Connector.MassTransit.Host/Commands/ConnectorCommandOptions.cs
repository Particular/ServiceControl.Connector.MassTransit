namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;

public static class ConnectorCommandOptions
{
    static Option<string> ConnectionString { get; } = new("--connection-string")
    {
        Description = "The connection string for the transport. Can also be set via CONNECTION_STRING environment variable."
    };

    static Option<string> TransportType { get; } = new("--transport-type")
    {
        Description = "The transport type (AzureServiceBus, AzureServiceBusWithDeadLetter, RabbitMQ, AmazonSQS). Can also be set via TRANSPORT_TYPE environment variable."
    };

    static Option<string> QueuesFile { get; } = new("--queues-file")
    {
        Description = "Path to the file containing the list of queues to monitor. Can also be set via QUEUES_FILE environment variable."
    };

    static Option<string> ReturnQueue { get; } = new("--return-queue")
    {
        Description = "The return queue name. Defaults to 'Particular.ServiceControl.Connector.MassTransit_return'. Can also be set via RETURN_QUEUE environment variable."
    };

    static Option<string> ErrorQueue { get; } = new("--error-queue")
    {
        Description = "The error queue name. Defaults to 'error'. Can also be set via ERROR_QUEUE environment variable."
    };

    static Option<string> ServiceControlQueue { get; } = new("--servicecontrol-queue")
    {
        Description = "The ServiceControl queue name. Defaults to 'Particular.ServiceControl'. Can also be set via SERVICECONTROL_QUEUE environment variable."
    };

    static Option<string> RabbitMqManagementApiUrl { get; } = new("--rabbitmq-management-api-url")
    {
        Description = "The RabbitMQ management API URL (required for RabbitMQ transport). Can also be set via RABBITMQ_MANAGEMENT_API_URL environment variable."
    };

    static Option<string> RabbitMqManagementApiUsername { get; } = new("--rabbitmq-management-api-username")
    {
        Description = "The RabbitMQ management API username. Can also be set via RABBITMQ_MANAGEMENT_API_USERNAME environment variable."
    };

    static Option<string> RabbitMqManagementApiPassword { get; } = new("--rabbitmq-management-api-password")
    {
        Description = "The RabbitMQ management API password. Can also be set via RABBITMQ_MANAGEMENT_API_PASSWORD environment variable."
    };

    static Option<string> RabbitMqQueueType { get; } = new("--rabbitmq-queue-type")
    {
        Description = "The RabbitMQ queue type (Classic or Quorum). Defaults to Quorum. Can also be set via RABBITMQ_QUEUE_TYPE environment variable."
    };

    public static void AddConnectorOptions(this Command command)
    {
        command.Add(ConnectionString);
        command.Add(TransportType);
        command.Add(QueuesFile);
        command.Add(ReturnQueue);
        command.Add(ErrorQueue);
        command.Add(ServiceControlQueue);
        command.Add(RabbitMqManagementApiUrl);
        command.Add(RabbitMqManagementApiUsername);
        command.Add(RabbitMqManagementApiPassword);
        command.Add(RabbitMqQueueType);
    }

    public static string[] BuildArgs(ParseResult parseResult)
    {
        var args = new List<string>();

        AddArgIfExplicit(ConnectionString, "ConnectionString");
        AddArgIfExplicit(TransportType, "TransportType");
        AddArgIfExplicit(QueuesFile, "QueuesFile");
        AddArgIfExplicit(ReturnQueue, "ReturnQueue");
        AddArgIfExplicit(ErrorQueue, "ErrorQueue");
        AddArgIfExplicit(ServiceControlQueue, "ServiceControlQueue");
        AddArgIfExplicit(RabbitMqManagementApiUrl, "RabbitMqManagementApiUrl");
        AddArgIfExplicit(RabbitMqManagementApiUsername, "RabbitMqManagementApiUsername");
        AddArgIfExplicit(RabbitMqManagementApiPassword, "RabbitMqManagementApiPassword");
        AddArgIfExplicit(RabbitMqQueueType, "RabbitMqQueueType");

        return [.. args];

        void AddArgIfExplicit(Option<string> option, string configKey)
        {
            // Only forward options that the user explicitly provided on the command line.
            // Omitting defaults lets environment variables take effect through normal config precedence.
            var token = parseResult.GetValue(option);
            if (token == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(token))
            {
                args.Add($"--{configKey}={token}");
            }
        }
    }
}