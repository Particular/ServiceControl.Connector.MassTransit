public partial class TestSuiteConfiguration
{
    public IConfigureTransportTestExecution CreateTransportConfiguration() => throw new NotImplementedException();
    public Task Cleanup() => Task.CompletedTask;
}