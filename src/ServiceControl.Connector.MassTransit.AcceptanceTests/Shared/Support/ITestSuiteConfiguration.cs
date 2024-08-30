public interface ITestSuiteConfiguration
{
    IConfigureTransportTestExecution CreateTransportConfiguration();
    Task Cleanup();
}
