using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

class AzureServiceBusHelper(ILogger<AzureServiceBusHelper> logger, string connectionstring) : IQueueInformationProvider, IQueueLengthProvider
{
    readonly ServiceBusAdministrationClient client = new(connectionstring);

    public async IAsyncEnumerable<string> GetQueues([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var result = client.GetQueuesAsync(cancellationToken);

        await foreach (var queueProperties in result)
        {
            if (queueProperties.RequiresSession)
            {
                logger.LogDebug("Skipping '{QueueName}', Queues that require sessions are currently unsupported",
                    queueProperties.Name);
                continue;
            }

            yield return queueProperties.Name;
        }
    }

    public async Task<bool> QueueExists(string queueName, CancellationToken cancellationToken) => (await client.QueueExistsAsync(queueName, cancellationToken)).Value;

    public async Task<long> GetQueueLength(string name, CancellationToken cancellationToken)
    {
        var queuesRuntimeProperties = await client.GetQueueRuntimePropertiesAsync(name, cancellationToken);
        return queuesRuntimeProperties.Value.ActiveMessageCount;
    }
}