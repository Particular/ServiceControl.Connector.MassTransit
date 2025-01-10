using System;
using Microsoft.Extensions.Logging;
using NServiceBus.AcceptanceTesting;

public class ScenarioContextLogger(string categoryName, ScenarioContext scenarioContext) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        scenarioContext.AddTrace($"{categoryName}: {formatter(state, exception)} - {exception}");
    }

    sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        NullScope()
        {
        }

        public void Dispose()
        {
        }
    }
}