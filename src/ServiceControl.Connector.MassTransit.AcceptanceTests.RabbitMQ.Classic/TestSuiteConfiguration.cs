[assembly: RabbitMQTest]

public partial class TestSuiteConfiguration
{
    public IConfigureTransportTestExecution CreateTransportConfiguration() => new ConfigureRabbitMQTransportTestExecution(QueueType.Classic);
    public Task Cleanup() => Task.CompletedTask;
}