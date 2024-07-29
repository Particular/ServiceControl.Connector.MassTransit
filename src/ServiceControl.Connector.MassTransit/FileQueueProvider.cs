public sealed class FileBasedQueueInformationProvider(string path) : IQueueInformationProvider
{
    public async Task<IEnumerable<string>> GetQueues()
    {
        // Read every time allows file to be updated without requiring to restart the host

        // TODO: Could be combined with transport implementation to filter out non-existing queues
        return await File.ReadAllLinesAsync(path);
    }
}