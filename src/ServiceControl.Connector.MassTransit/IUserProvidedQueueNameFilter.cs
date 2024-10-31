namespace ServiceControl.Adapter.MassTransit;

public interface IUserProvidedQueueNameFilter
{
    bool IsMatch(string queueName);
}