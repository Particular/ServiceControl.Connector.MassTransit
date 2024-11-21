namespace ServiceControl.Connector.MassTransit;

public interface IUserProvidedQueueNameFilter
{
    bool IsMatch(string queueName);
}