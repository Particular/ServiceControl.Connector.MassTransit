[assembly: RabbitMQTest]

public partial class TestSuiteConfiguration
{
    public IConfigureTransportTestExecution CreateTransportConfiguration() => new ConfigureRabbitMQTransportTestExecution();
    public Task Cleanup() => Task.CompletedTask;
}

