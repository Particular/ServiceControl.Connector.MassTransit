public interface IQueueInformationProvider
{
    Task<IEnumerable<string>> GetQueues();
}
