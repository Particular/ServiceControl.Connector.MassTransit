public record Configuration
{
  public required string ErrorQueue { get; init; }
  public required string ReturnQueue { get; init; }
  public required bool SetupInfrastructure { get; init; }
  public string PoisonQueue => ReturnQueue + ".poison";
}
