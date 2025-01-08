public interface IFileBasedQueueInformationProvider
{
    Task<IEnumerable<string>> GetQueues(CancellationToken cancellationToken);
}

public sealed class FileBasedQueueInformationProvider(string path) : IFileBasedQueueInformationProvider
{
    public async Task<IEnumerable<string>> GetQueues(CancellationToken cancellationToken) =>
        // Read every time allows file to be updated without requiring to restart the host
        await File.ReadAllLinesAsync(path, cancellationToken);
}