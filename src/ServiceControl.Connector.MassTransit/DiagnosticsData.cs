using System.Collections.Concurrent;

public class DiagnosticsData
{
    static readonly object CleanupLock = new();
    static readonly object ErrorLock = new();
    static readonly object NotFoundLock = new();

    const int BufferLogEntriesSize = 100;

    readonly HashSet<string> errorQueues = [];
    readonly List<string> notFoundQueues = [];

    public ConcurrentQueue<LogEntry> RecentLogEntries { get; } = new();

    public string[] MassTransitErrorQueues
    {
        get
        {
            lock (ErrorLock)
            {
                return errorQueues.ToArray();
            }
        }
    }

    public void AddErrorQueues(HashSet<string> queues)
    {
        lock (ErrorLock)
        {
            errorQueues.Clear();
            errorQueues.UnionWith(queues);
        }
    }

    public string[] MassTransitNotFoundQueues
    {
        get
        {
            lock (NotFoundLock)
            {
                return notFoundQueues.ToArray();
            }
        }
    }

    public void AddNotFound(string queueName)
    {
        lock (NotFoundLock)
        {
            notFoundQueues.Add(queueName);
        }
    }

    public void ClearNotFound()
    {
        lock (NotFoundLock)
        {
            notFoundQueues.Clear();
        }
    }

    public void AddLog(DateTimeOffset date, string level, string message)
    {
        RecentLogEntries.Enqueue(new LogEntry(date, level, message));

        lock (CleanupLock)
        {
            while (RecentLogEntries.Count > BufferLogEntriesSize)
            {
                if (!RecentLogEntries.TryDequeue(out _))
                {
                    break;
                }
            }
        }
    }

    public class LogEntry(DateTimeOffset dateTime, string level, string message)
    {
        public string Message { get; init; } = message;
        public DateTimeOffset Date { get; init; } = dateTime;
        public string Level { get; init; } = level;
    }
}