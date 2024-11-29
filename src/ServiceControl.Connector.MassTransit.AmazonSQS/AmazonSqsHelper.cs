using Amazon.SQS;
using Amazon.SQS.Model;

class AmazonSqsHelper(string? queueNamePrefix = null) : IQueueInformationProvider
{
#pragma warning disable PS0018
    public async Task<IEnumerable<string>> GetQueues()
#pragma warning restore PS0018
    {
        var client = new AmazonSQSClient();

        var list = new List<string>();

        var request = new ListQueuesRequest { QueueNamePrefix = queueNamePrefix, MaxResults = 1000 };
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
}