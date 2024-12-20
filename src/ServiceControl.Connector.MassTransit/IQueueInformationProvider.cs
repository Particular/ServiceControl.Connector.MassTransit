public interface IQueueInformationProvider
{
    Task<IEnumerable<string>> GetQueues(CancellationToken cancellationToken);
}