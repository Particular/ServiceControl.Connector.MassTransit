using Microsoft.Extensions.Logging;

sealed class RabbitMQMassTransitFailureAdapter(
    ILogger<MassTransitFailureAdapter> logger,
    MassTransitConverter mtConverter,
    Configuration configuration
)
    : MassTransitFailureAdapter(
        logger,
        mtConverter,
        configuration
    )
{
    protected override string QueuePrefix => "exchange:";
}