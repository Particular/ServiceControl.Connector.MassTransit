public sealed class AllQueueFilter : IQueueFilter
{
    public bool IsMatch(string queue)
    {
        return true;
    }
}