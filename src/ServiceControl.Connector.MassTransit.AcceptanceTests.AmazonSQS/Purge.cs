using Amazon.SimpleNotificationService;
using Amazon.SQS;
using NUnit.Framework;

[TestFixture, Explicit]
public class Purge
{
    [Test]
    public async Task DeleteQueues()
    {
        var c = new AmazonSQSClient();
        var response = await c.ListQueuesAsync(string.Empty);
        foreach (var queueUrl in response.QueueUrls)
        {
            await c.DeleteQueueAsync(queueUrl);
        }
    }
    [Test]
    public async Task DeleteTopics()
    {
        var c = new AmazonSimpleNotificationServiceClient();
        var response = await c.ListTopicsAsync();
        foreach (var topic in response.Topics)
        {
            await c.DeleteTopicAsync(topic.TopicArn);
        }
    }
    [Test]
    public async Task Unsubscribes()
    {
        var c = new AmazonSimpleNotificationServiceClient();
        var response = await c.ListSubscriptionsAsync();
        foreach (var subscription in response.Subscriptions)
        {
            await c.UnsubscribeAsync(subscription.SubscriptionArn);
        }
    }
}