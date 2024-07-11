using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NServiceBus.Faults;
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

  public virtual TransportOperation ForwardMassTransitErrorToServiceControl(MessageContext messageContext)
  {
    var md5 = Convert.ToHexString(MD5.HashData(messageContext.Body.Span));

    logger.LogInformation("Forwarding failure: {NativeMessageId}, md5: {MD5}, length: {PayloadLength:N0}b", messageContext.NativeMessageId, md5, messageContext.Body.Length);
    mtConverter.From(messageContext);

    messageContext.Headers[RetryTo] = configuration.ReturnQueue;

    if (!messageContext.Headers.TryGetValue(Headers.MessageId, out var messageId))
    {
      messageId = messageContext.NativeMessageId;
    }

    var request = new OutgoingMessage(
      messageId: messageId,
      headers: messageContext.Headers,
      body: messageContext.Body
    );
    var operation = new TransportOperation(request, new UnicastAddressTag(configuration.ErrorQueue));
    return operation;
  }

  public virtual TransportOperation ReturnMassTransitFailure(MessageContext messageContext)
  {
    var md5 = Convert.ToHexString(MD5.HashData(messageContext.Body.Span));
    logger.LogInformation("Forward back to original MT queue {NativeMessageId}, md5: {MD5}, length: {PayloadLength:N0}b", messageContext.NativeMessageId, md5, messageContext.Body.Length);

    if (!messageContext.Headers.TryGetValue(Headers.MessageId, out var messageId))
    {
      messageId = messageContext.NativeMessageId;
    }

    var faultInputAddress = new Uri(messageContext.Headers[MassTransit.MessageHeaders.FaultInputAddress]);
    var originalQueue = faultInputAddress.LocalPath;
    originalQueue = originalQueue.Substring(originalQueue.LastIndexOf('/') + 1);

    if (messageContext.Headers.TryGetValue(TargetEndpointAddress, out var targetEndpointAddress))
    {
      // This header is set when ServiceControl has a queue redirect
      originalQueue = targetEndpointAddress;
    }
    
    messageContext.Headers.TryGetValue(Headers.ContentType, out var contentType);

    mtConverter.To(messageContext); // Should remove any NServiceBus added header
    
    logger.LogInformation("{FaultInputAddress} => {Queue}", faultInputAddress, originalQueue);

    var request = new OutgoingMessage(messageId: messageId, headers: messageContext.Headers, body: messageContext.Body);
    var operation = new TransportOperation(request, new UnicastAddressTag(originalQueue));

    // RabbitMQ and AzureServiceBus
    if (contentType != null)
    {
      operation.Properties.Add(Headers.ContentType, contentType);
    }

    // AzureServiceBus
    // TODO: AzureServiceBus  operation.DisableLegacyHeaders();
    // RabbitMQ and AzureServiceBus
    operation.Properties[Headers.MessageId] = messageId; // MassTransit sets native message ID to logical message id value

    return operation;
  }
}
