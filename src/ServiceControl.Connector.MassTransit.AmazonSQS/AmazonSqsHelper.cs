using System.Collections.Concurrent;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.Transport;

sealed class AmazonSqsHelper(SqsTransport transportDefinition) : IQueueInformationProvider, IQueueLengthProvider
{
    readonly SqsTransport transportDefinition = transportDefinition;
    readonly ConcurrentDictionary<string, Task<string>> queueUrlCache = new();

#pragma warning disable PS0018
    public async Task<IEnumerable<string>> GetQueues()
#pragma warning restore PS0018
    {
        var client = new AmazonSQSClient();

        var list = new List<string>();

        var request = new ListQueuesRequest { QueueNamePrefix = transportDefinition.QueueNamePrefix, MaxResults = 1000 };
        ListQueuesResponse response;
        do
        {
            response = await client.ListQueuesAsync(request);
            foreach (var queueUrl in response.QueueUrls)
            {
                var queue = queueUrl.Substring(queueUrl.LastIndexOf('/') + 1);
                list.Add(queue);
            }
        } while (null != (request.NextToken = response.NextToken));

        return list;
    }

    public async Task<long> GetQueueLength(string name, CancellationToken cancellationToken = default)
    {
        var client = new AmazonSQSClient();

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