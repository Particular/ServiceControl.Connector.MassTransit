public sealed class AcceptanceTestQueueFilter : IQueueFilter
{
    public bool IsMatch(string x)
    {
        return (x.EndsWith("_error") || x.EndsWith("_skipped")) && x.StartsWith(NamePrefixGenerator.GetNamePrefix());
    }
}