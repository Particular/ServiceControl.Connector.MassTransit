namespace ServiceControl.Connector.MassTransit;

public class MassTransitConnectorHeartbeat : IMessage
{
    public required string Version { get; init; }
    public required ErrorQueue[] ErrorQueues { get; init; }
    public required LogEntry[] Logs { get; init; }
    public required DateTimeOffset SentDateTimeOffset { get; init; }
}

#pragma warning disable CA1711
public class ErrorQueue
#pragma warning restore CA1711
{
    public required string Name { get; init; }
    public required bool Ingesting { get; init; }
}

public class LogEntry(DateTimeOffset dateTime, string level, string message)
{
    public string Message { get; init; } = message;
    public DateTimeOffset Date { get; init; } = dateTime;
    public string Level { get; init; } = level;
}