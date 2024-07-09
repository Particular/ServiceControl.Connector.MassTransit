using Microsoft.Extensions.Logging;
using NServiceBus.Transport;
using NServiceBus.Transport.SQS.Experimental;

sealed class AmazonSqsMassTransitFailureAdapter(
    ILogger<MassTransitFailureAdapter> logger,
    MassTransitConverter mtConverter,
    Configuration configuration
)
    : MassTransitFailureAdapter
    (
        logger,
        mtConverter,
        configuration
    )
{
    public override TransportOperation ReturnMassTransitFailure(MessageContext messageContext, IMessageDispatcher messageDispatcher,
        CancellationToken cancellationToken)
    {
        var operation = base.ReturnMassTransitFailure(messageContext, messageDispatcher, cancellationToken);
#pragma warning disable CS0618 // Type or member is obsolete
        operation.UseFlatHeaders();
#pragma warning restore CS0618 // Type or member is obsolete
        return operation;
    }
}