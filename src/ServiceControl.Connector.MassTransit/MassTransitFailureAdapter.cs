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
  public virtual TransportOperation ForwardMassTransitErrorToServiceControl(
    MessageContext messageContext,
    IMessageDispatcher messageDispatcher,
    CancellationToken cancellationToken
  )
  {
    var md5 = Convert.ToHexString(MD5.HashData(messageContext.Body.Span));

    logger.LogInformation("Forwarding failure: {NativeMessageId}, md5: {MD5}, length: {PayloadLength:N0}b", messageContext.NativeMessageId, md5, messageContext.Body.Length);
    mtConverter.From(messageContext);

    messageContext.Headers[FaultsHeaderKeys.FailedQ] = configuration.ReturnQueue;

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

  public virtual TransportOperation ReturnMassTransitFailure(
    MessageContext messageContext,
    IMessageDispatcher messageDispatcher,
    CancellationToken cancellationToken
  )
  {
    var md5 = Convert.ToHexString(MD5.HashData(messageContext.Body.Span));
    logger.LogInformation("Forward back to original MT queue {NativeMessageId}, md5: {MD5}, length: {PayloadLength:N0}b", messageContext.NativeMessageId, md5, messageContext.Body.Length);

    if (!messageContext.Headers.TryGetValue(Headers.MessageId, out var messageId))
    {
      messageId = messageContext.NativeMessageId;
    }

    messageContext.Headers.TryGetValue(Headers.ContentType, out var contentType);

    mtConverter.To(messageContext); // Should remove any NServiceBus added header

    var faultInputAddress = new Uri(messageContext.Headers[MassTransit.MessageHeaders.FaultInputAddress]);

    var originalQueue = faultInputAddress.LocalPath;
    originalQueue = originalQueue.Substring(originalQueue.LastIndexOf('/') + 1);

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
