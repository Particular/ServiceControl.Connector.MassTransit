using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Amazon.SQS;
using Amazon.SQS.Model;

sealed class AmazonSqsHelper(IAmazonSQS client, SqsTransport transportDefinition, string? queueNamePrefix = null) : IQueueInformationProvider, IQueueLengthProvider
{
    readonly ConcurrentDictionary<string, Task<string>> queueUrlCache = new();

    public async IAsyncEnumerable<string> GetQueues([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new ListQueuesRequest { QueueNamePrefix = queueNamePrefix, MaxResults = 1000 };
        ListQueuesResponse response;

        do
        {
            response = await client.ListQueuesAsync(request, cancellationToken);

            foreach (var queue in response.QueueUrls.Select(GetQueueNameFromUrl))
            {
                yield return queue;
            }
        } while ((request.NextToken = response.NextToken) is not null);

        yield break;

        static string GetQueueNameFromUrl(string url)
        {
            // Example of a queue url, https://sqs.us-east-1.amazonaws.com/123456789012/my-queue-name
            return url.Split('/').Last();
        }
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

    public async Task<long> GetQueueLength(string name, CancellationToken cancellationToken)
    {
        var queueUrl = await queueUrlCache.GetOrAdd(name, async (k) =>
        {
            var queueName = transportDefinition.QueueNameGenerator(name, transportDefinition.QueueNamePrefix);
            var getQueueUrlResponse = await client.GetQueueUrlAsync(queueName, cancellationToken);
            return getQueueUrlResponse.QueueUrl;
        });

        var attReq = new GetQueueAttributesRequest { QueueUrl = queueUrl };
        attReq.AttributeNames.Add("ApproximateNumberOfMessages");

        var attResponse = await client.GetQueueAttributesAsync(attReq, cancellationToken);
        var value = attResponse.ApproximateNumberOfMessages;

        return value;
    }
}