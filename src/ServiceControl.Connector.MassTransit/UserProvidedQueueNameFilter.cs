namespace ServiceControl.Adapter.MassTransit;

using System.Text.RegularExpressions;

public class UserProvidedQueueNameFilter(string? filter) : IUserProvidedQueueNameFilter
{
    public bool IsMatch(string queueName)
    {
        if (!string.IsNullOrEmpty(filter))
        {
            return Regex.IsMatch(queueName, filter);
        }

        return true;
    }
}