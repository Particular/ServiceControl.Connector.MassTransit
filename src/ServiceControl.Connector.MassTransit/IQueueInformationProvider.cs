public interface IQueueInformationProvider
{
    IAsyncEnumerable<string> GetQueues(CancellationToken cancellationToken);
    Task<bool> QueueExists(string queueName, CancellationToken cancellationToken);
}