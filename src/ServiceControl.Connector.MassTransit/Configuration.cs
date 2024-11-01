using ServiceControl.Adapter.MassTransit;

public record Configuration
{
    public required string ErrorQueue { get; init; }
    public required string ReturnQueue { get; init; }
    public required string ControlQueue { get; init; }
    public required Command Command { get; init; }
    public string PoisonQueue => ReturnQueue + ".poison";
    public TimeSpan QueueScanInterval { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxRetries => 15;

    public bool IsSetup => Command is Command.Setup or Command.SetupAndRun;
    public bool IsRun => Command is Command.Run or Command.SetupAndRun;
}
