public interface IQueueFilter
{
    bool IsMatch(string queue);
}