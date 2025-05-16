using Microsoft.Extensions.Logging;
using NServiceBus.Routing;
using NServiceBus.Transport;

public class MassTransitFailureAdapter(
  ILogger<MassTransitFailureAdapter> logger,
  MassTransitConverter mtConverter,
  Configuration configuration
)
{
    // https://github.com/Particular/ServiceControl/blob/5.4.0/src/ServiceControl/Recoverability/Retrying/Infrastructure/ReturnToSender.cs#L40-L57
    const string TargetEndpointAddress = "ServiceControl.TargetEndpointAddress";
    const string RetryTo = "ServiceControl.RetryTo";

    public const string ContentTypeKey = "Content-Type"; // As MIME spec
    public const string MessageIdKey = "Message-ID"; // As MIME spec

    protected virtual string QueuePrefix => "queue:";

    public virtual TransportOperation ForwardMassTransitErrorToServiceControl(MessageContext messageContext)
    {
        logger.LogInformation("Forwarding failure with {NativeMessageId} native message id from {SourceAddress} to {ServiceControlErrorQueue}", messageContext.NativeMessageId, messageContext.ReceiveAddress, configuration.ErrorQueue);

        mtConverter.From(messageContext);

        Dictionary<string, string> headers = messageContext.Headers;

        headers[RetryTo] = configuration.ReturnQueue;

        if (!headers.TryGetValue(Headers.MessageId, out var messageId))
        {
            messageId = messageContext.NativeMessageId;
        }

        var request = new OutgoingMessage(
            messageId: messageId,
            headers: headers,
            body: messageContext.Body
        );
        var operation = new TransportOperation(request, new UnicastAddressTag(configuration.ErrorQueue));
        return operation;
    }

    public virtual TransportOperation ReturnMassTransitFailure(MessageContext messageContext)
    {
        var headers = messageContext.Headers;

        if (!headers.TryGetValue(Headers.MessageId, out var messageId))
        {
            messageId = messageContext.NativeMessageId;
        }

        if (!headers.TryGetValue(TargetEndpointAddress, out var targetEndpointAddress))
        {
            throw new InvalidOperationException($"Header '{TargetEndpointAddress}' is not set");
        }

        string originalQueue;

        // If not Uri, assuming plain queue name due to queue redirect
        if (Uri.IsWellFormedUriString(targetEndpointAddress, UriKind.Absolute))
        {
            var faultInputAddress = new Uri(targetEndpointAddress);
            originalQueue = faultInputAddress.LocalPath;
            originalQueue = originalQueue.Substring(originalQueue.LastIndexOf('/') + 1);
        }
        else
        {
            originalQueue = targetEndpointAddress;
        }

        logger.LogInformation("Forwarding failure with {NativeMessageId} native message id from {FaultInputAddress} back to original MassTransit queue {MassTransitQueue}", messageContext.NativeMessageId, targetEndpointAddress, originalQueue);

        messageContext.Headers.TryGetValue(Headers.ContentType, out var contentType);

        mtConverter.To(messageContext); // Should remove any NServiceBus added header

        var request = new OutgoingMessage(messageId: messageId, headers: messageContext.Headers, body: messageContext.Body);
        var operation = new TransportOperation(request, new UnicastAddressTag(originalQueue));

        // RabbitMQ and AzureServiceBus
        if (contentType != null)
        {
            operation.Properties.Add(ContentTypeKey, contentType);
        }

        PatchAckQueue(operation);

        // RabbitMQ and AzureServiceBus
        operation.Properties[MessageIdKey] = messageId; // MassTransit sets native message ID to logical message id value

        return operation;
    }

    protected virtual void PatchAckQueue(TransportOperation operation)
    {
        const string RetryConfirmationQueueHeaderKey = "ServiceControl.Retry.AcknowledgementQueue";
        var h = operation.Message.Headers;
        if (!h.TryGetValue(RetryConfirmationQueueHeaderKey, out var ackQueue))
        {
            throw new InvalidOperationException($"Message is expected to have '{RetryConfirmationQueueHeaderKey}' header");
        }

        ackQueue = QueuePrefix + ackQueue;
        h[RetryConfirmationQueueHeaderKey] = ackQueue;
    }
}