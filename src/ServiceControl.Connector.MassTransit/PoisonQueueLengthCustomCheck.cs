using Microsoft.Extensions.Hosting;
using NServiceBus.CustomChecks;

class PoisonQueueLengthCustomCheck : CustomCheck
{
    readonly Configuration config;
    readonly IQueueLengthProvider queueLengthProvider;
    readonly IHostApplicationLifetime applicationLifetime;

    public PoisonQueueLengthCustomCheck(Configuration config, IQueueLengthProvider queueLengthProvider, IHostApplicationLifetime applicationLifetime)
        : base("Poison queue", "MassTransit", config.CustomChecksInterval)
    {
        this.config = config;
        this.queueLengthProvider = queueLengthProvider;
        this.applicationLifetime = applicationLifetime;
    }

    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        if (!applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return CheckResult.Pass; //We can check the queue length only when the app fully started and created necessary queues.
        }

        var poisonQueue = config.PoisonQueue;

        var length = await queueLengthProvider.GetQueueLength(poisonQueue, cancellationToken);

        var isEmpty = length == 0;

        return isEmpty
            ? CheckResult.Pass
            : CheckResult.Failed($"Queue `{poisonQueue}` has {length} messages that could not be forwarded to ServiceControl.");
    }
}