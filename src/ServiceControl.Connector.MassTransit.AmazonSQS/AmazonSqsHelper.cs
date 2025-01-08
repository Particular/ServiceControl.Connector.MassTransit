using System.Runtime.CompilerServices;
using Amazon.SQS;
using Amazon.SQS.Model;

sealed class AmazonSqsHelper(IAmazonSQS client, string? queueNamePrefix = null) : IQueueInformationProvider
{
    public async IAsyncEnumerable<string> GetQueues([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new ListQueuesRequest { QueueNamePrefix = queueNamePrefix, MaxResults = 1000 };
        ListQueuesResponse response;

        do
        {
            response = await client.ListQueuesAsync(request, cancellationToken);
            foreach (var queue in response.QueueUrls.Select(url => url.Split('/')[4]))
            {
                yield return queue;
            }
        } while ((request.NextToken = response.NextToken) is not null);

    }

    public async Task<bool> QueueExists(string queueName, CancellationToken cancellationToken)
    {
        try
        {
            await client.GetQueueUrlAsync(queueName, cancellationToken);
            return true;
        }
        catch (QueueDoesNotExistException)
        {
            // Nothing to be done here
        }
        return false;
    }
}