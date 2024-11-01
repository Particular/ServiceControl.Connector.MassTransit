namespace ServiceControl.Monitoring;

public class ConnectedApplication : IMessage
{
    public required string Application { get; init; }
    public required string[] ErrorQueues { get; init; }
}
