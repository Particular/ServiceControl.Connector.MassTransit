public sealed class FileBasedQueueInformationProvider(string path) : IQueueInformationProvider
{
    public async Task<IEnumerable<string>> GetQueues(CancellationToken cancellationToken) =>
        // Read every time allows file to be updated without requiring to restart the host
        await File.ReadAllLinesAsync(path, cancellationToken);
}