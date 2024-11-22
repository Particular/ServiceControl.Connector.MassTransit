using Amazon.SQS;
using Amazon.SQS.Model;

sealed class AmazonSqsHelper(string? queueNamePrefix = null) : IQueueInformationProvider
{
#pragma warning disable PS0018
    public async Task<IEnumerable<string>> GetQueues()
#pragma warning restore PS0018
    {
        var client = new AmazonSQSClient();

        var request = new ListQueuesRequest { QueueNamePrefix = queueNamePrefix };
        var response = await client.ListQueuesAsync(request);

        var list = new List<string>();

        foreach (var queueUrl in response.QueueUrls)
        {
            var queue = queueUrl.Substring(queueUrl.LastIndexOf('/') + 1);
            list.Add(queue);
        }

        return list;
    }
}