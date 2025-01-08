namespace ServiceControl.Connector.MassTransit.AcceptanceTesting;

using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;

public class ScenarioContextLoggerProvider(ScenarioContext scenarioContext) : ILoggerProvider
{
    public ILogger CreateLogger(string name) => new ScenarioContextLogger(name, scenarioContext);

    public void Dispose()
    {
    }
}