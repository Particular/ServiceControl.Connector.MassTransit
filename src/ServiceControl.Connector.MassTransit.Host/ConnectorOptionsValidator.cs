namespace ServiceControl.Connector.MassTransit.Host;

using Microsoft.Extensions.Options;

public class ConnectorOptionsValidator : IValidateOptions<ConnectorOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectorOptions options)
    {
        var failures = new List<string>();

        // ConnectionString is required for all transports except AmazonSQS (which uses IAM credentials).
        // Note: [Required] is intentionally absent from ConnectorOptions.ConnectionString.
        if (options.TransportType != "AmazonSQS" && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("CONNECTION_STRING is required for this transport. Use --connection-string or set the CONNECTION_STRING environment variable.");
        }

        // RabbitMQ-specific validation — DataAnnotations can't handle conditional validation.
        if (options.TransportType == "RabbitMQ")
        {
            if (string.IsNullOrWhiteSpace(options.RabbitMqManagementApiUrl))
            {
                failures.Add("RABBITMQ_MANAGEMENT_API_URL is required when using RabbitMQ transport. Use --rabbitmq-management-api-url or set the RABBITMQ_MANAGEMENT_API_URL environment variable.");
            }
            else if (!Uri.TryCreate(options.RabbitMqManagementApiUrl, UriKind.Absolute, out _))
            {
                failures.Add("RABBITMQ_MANAGEMENT_API_URL is invalid. Ensure the value is a valid URL without any quotes (e.g., http://localhost:15672).");
            }

            if (!IsValidQueueType(options.RabbitMqQueueType))
            {
                failures.Add($"RABBITMQ_QUEUE_TYPE '{options.RabbitMqQueueType}' is invalid. Must be one of: Classic, Quorum");
            }
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    static bool IsValidQueueType(string? queueType) =>
        queueType?.ToLowerInvariant() switch
        {
            "classic" or "quorum" or
            null => true, // Default will be used
            _ => false
        };
}
