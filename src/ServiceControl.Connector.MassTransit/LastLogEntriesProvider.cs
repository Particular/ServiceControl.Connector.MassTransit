using Microsoft.Extensions.Logging;

public class LastLogEntriesProvider(DiagnosticsData diagnosticsData) : ILoggerProvider
{
    public void Dispose()
    {

    }

    public ILogger CreateLogger(string categoryName) => new LastLogEntriesLogger(categoryName, diagnosticsData);


    class LastLogEntriesLogger(string name, DiagnosticsData diagnosticsData) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Warning)
            {
                return;
            }

            string message = formatter(state, exception);

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            message = $"[{name}]{logLevel}: {message}";

            if (exception != null)
            {
                message += Environment.NewLine + Environment.NewLine + exception;
            }

            diagnosticsData.AddLog(message);
        }
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