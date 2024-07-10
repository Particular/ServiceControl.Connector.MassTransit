using System.Linq.Expressions;

public interface IQueueFilter
{
    bool IsMatch(string queue);
}