namespace ServiceControl.Connector.MassTransit.Host;

using System.ComponentModel.DataAnnotations;

public class ConnectorOptions
{
    // Not [Required]: AmazonSQS uses IAM credentials and does not need a connection string.
    // Validated conditionally in ConnectorOptionsValidator for other transports.
    public string ConnectionString { get; set; } = string.Empty;

    [Required(ErrorMessage = "TRANSPORT_TYPE is required")]
    [RegularExpression("^(AzureServiceBus|AzureServiceBusWithDeadLetter|RabbitMQ|AmazonSQS)$", ErrorMessage = "TRANSPORT_TYPE must be one of: AzureServiceBus, AzureServiceBusWithDeadLetter, RabbitMQ, AmazonSQS")]
    public string TransportType { get; set; } = string.Empty;

    // Not [Required]: setup-only mode does not need a queues file.
    // Validated conditionally in HostApplicationBuilderExtensions when not in setup-only mode.
    public string QueuesFile { get; set; } = string.Empty;

    public string ReturnQueue { get; set; } = "Particular.ServiceControl.Connector.MassTransit_return";

    public string ErrorQueue { get; set; } = "error";

    public string ServiceControlQueue { get; set; } = "Particular.ServiceControl";

    public string? RabbitMqManagementApiUrl { get; set; }

    public string? RabbitMqManagementApiUsername { get; set; }

    public string? RabbitMqManagementApiPassword { get; set; }

    public string? RabbitMqQueueType { get; set; } = "Quorum";
}
