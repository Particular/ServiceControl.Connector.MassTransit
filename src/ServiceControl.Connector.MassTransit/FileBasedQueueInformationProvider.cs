public interface IFileBasedQueueInformationProvider
{
    IAsyncEnumerable<string> GetQueues(CancellationToken cancellationToken);
}

public sealed class FileBasedQueueInformationProvider(string path) : IFileBasedQueueInformationProvider
{
    public IAsyncEnumerable<string> GetQueues(CancellationToken cancellationToken) =>
        // Read every time allows file to be updated without requiring to restart the host
        File.ReadLinesAsync(path, cancellationToken);
}