public sealed class ErrorAndSkippedQueueFilter : IQueueFilter
{
    public bool IsMatch(string x)
    {
        return x.EndsWith("_error") || x.EndsWith("_skipped");
    }
}