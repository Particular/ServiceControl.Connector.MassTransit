public sealed class FileBasedQueueInformationProvider(string path) : IQueueInformationProvider
{
#pragma warning disable PS0018
    public async Task<IEnumerable<string>> GetQueues()
#pragma warning restore PS0018
    {
        // Read every time allows file to be updated without requiring to restart the host

        // TODO: Could be combined with transport implementation to filter out non-existing queues
        return await File.ReadAllLinesAsync(path);
    }

#pragma warning disable PS0018
    public Task<long> GetQueueLength(string name)
#pragma warning restore PS0018
    {
        //TODO: I think we need to split these interfaces
        return Task.FromResult<long>(0);
    }
}