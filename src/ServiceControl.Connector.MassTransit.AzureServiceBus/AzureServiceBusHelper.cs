using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

class AzureServiceBusHelper(ILogger<AzureServiceBusHelper> logger, string connectionstring) : IQueueInformationProvider
{
    readonly ServiceBusAdministrationClient client = new(connectionstring);

#pragma warning disable PS0018
    public async Task<IEnumerable<string>> GetQueues()
#pragma warning restore PS0018
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
}
