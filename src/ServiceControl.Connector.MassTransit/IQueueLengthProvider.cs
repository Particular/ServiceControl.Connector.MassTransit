public interface IQueueLengthProvider
{
    Task<long> GetQueueLength(string name, CancellationToken cancellationToken = default);
}