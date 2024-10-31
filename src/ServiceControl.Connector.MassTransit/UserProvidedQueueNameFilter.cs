namespace ServiceControl.Adapter.MassTransit;

public class UserProvidedQueueNameFilter(string? filter, string? prefix, string? suffix) : IUserProvidedQueueNameFilter
{
    public bool IsMatch(string queueName)
    {
        if (!string.IsNullOrEmpty(filter))
        {
            return queueName.Contains(filter);
        }
        else if (!string.IsNullOrEmpty(prefix))
        {
            return queueName.StartsWith(prefix);
        }
        else if (!string.IsNullOrEmpty(suffix))
        {
            return queueName.EndsWith(suffix);
        }

        return true;
    }
}