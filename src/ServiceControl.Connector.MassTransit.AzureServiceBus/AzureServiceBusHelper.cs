using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

class AzureServiceBusHelper(ILogger<AzureServiceBusHelper> logger, string connectionstring) : IQueueInformationProvider
{
    readonly ServiceBusAdministrationClient client = new(connectionstring);

    async Task<IEnumerable<string>> IQueueInformationProvider.GetQueues(CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        var result = client.GetQueuesAsync(cancellationToken);

        await foreach (var queueProperties in result)
        {
            if (queueProperties.RequiresSession)
            {
                logger.LogDebug("Skipping '{QueueName}', Queues that require sessions are currently unsupported", queueProperties.Name);
                continue;
            }

            list.Add(queueProperties.Name);
        }
        return list;
    }
}