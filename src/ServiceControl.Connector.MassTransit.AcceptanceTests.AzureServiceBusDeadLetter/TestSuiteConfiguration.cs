[assembly: AzureServiceBusDeadLetterTest]

public partial class TestSuiteConfiguration
{
    public IConfigureTransportTestExecution CreateTransportConfiguration() => new ConfigureAzureServiceBusTransportTestExecution();
    public Task Cleanup() => Task.CompletedTask;
}