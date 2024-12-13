using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

class AzureServiceBusHelper(ILogger<AzureServiceBusHelper> logger, string connectionstring) : IQueueInformationProvider, IQueueLengthProvider
{
    readonly ServiceBusAdministrationClient client = new(connectionstring);

    async Task<IEnumerable<string>> IQueueInformationProvider.GetQueues()
    {
        var list = new List<string>();
        var result = client.GetQueuesAsync();

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

    public async Task<long> GetQueueLength(string name, CancellationToken cancellationToken = default)
    {
        var queuesRuntimeProperties = await client.GetQueueRuntimePropertiesAsync(name, cancellationToken);
        return queuesRuntimeProperties.Value.ActiveMessageCount;
    }
}