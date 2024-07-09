using Amazon.SQS;
using Amazon.SQS.Model;

class AmazonSqsHelper(string? queueNamePrefix = null) : IQueueInformationProvider
{
  public async Task<IEnumerable<string>> GetQueues()
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