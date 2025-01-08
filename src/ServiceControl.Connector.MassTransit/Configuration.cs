using ServiceControl.Connector.MassTransit;

public record Configuration
{
    public required string ErrorQueue { get; init; }
    public required string ReturnQueue { get; init; }
    public required Command Command { get; init; }
    public string PoisonQueue => ReturnQueue + ".poison";
    public string? UserProvidedQueueNameFilter { get; init; }
    public TimeSpan QueueScanInterval { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxRetries => 15;
}