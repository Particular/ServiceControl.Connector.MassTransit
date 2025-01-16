using System.Collections.Concurrent;

public class DiagnosticsData
{
    static readonly object CleanupLock = new();
    static readonly object ErrorLock = new();
    static readonly object NotFoundLock = new();

    const int BufferLogEntriesSize = 100;

    readonly HashSet<string> errorQueues = [];
    readonly List<string> notFoundQueues = [];

    public ConcurrentQueue<string> RecentLogEntries { get; } = new();

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

    public void AddLog(string entry)
    {
        RecentLogEntries.Enqueue(entry);

        lock (CleanupLock)
        {
            if (RecentLogEntries.Count > BufferLogEntriesSize)
            {
                RecentLogEntries.TryDequeue(out _);
            }
        }
    }
}