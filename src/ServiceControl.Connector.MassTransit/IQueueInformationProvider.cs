public interface IQueueInformationProvider
{
#pragma warning disable PS0018
    Task<IEnumerable<string>> GetQueues();
#pragma warning restore PS0018
}