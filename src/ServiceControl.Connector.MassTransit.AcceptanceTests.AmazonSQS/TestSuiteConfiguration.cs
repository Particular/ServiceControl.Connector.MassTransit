using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

[assembly: AmazonSQSTest]

public partial class TestSuiteConfiguration
{
    public IConfigureTransportTestExecution CreateTransportConfiguration() => new ConfigureAmazonSQSTransportTestExecution();

    public async Task Cleanup()
    {
        using (var sqsClient = new AmazonSQSClient())
        using (var snsClient = new AmazonSimpleNotificationServiceClient())
        using (var s3Client = new AmazonS3Client())
        {
            await SQSCleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefixGenerator.GetNamePrefix()).ConfigureAwait(false);
        }
    }
}