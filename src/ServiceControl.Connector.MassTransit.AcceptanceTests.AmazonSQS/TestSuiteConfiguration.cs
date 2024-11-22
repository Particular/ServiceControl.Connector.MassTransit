using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

[assembly: AmazonSQSTest]

public partial class TestSuiteConfiguration
{
    public IConfigureTransportTestExecution CreateTransportConfiguration() => new ConfigureAmazonSQSTransportTestExecution();
    public async Task Cleanup()
    {
        var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        using (var sqsClient = new AmazonSQSClient(accessKeyId, secretAccessKey))
        using (var snsClient = new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey))
        using (var s3Client = new AmazonS3Client(accessKeyId, secretAccessKey))
        {
            await SQSCleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefixGenerator.GetNamePrefix()).ConfigureAwait(false);
        }
    }
}