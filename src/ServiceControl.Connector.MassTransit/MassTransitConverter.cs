using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MassTransit;
using MassTransit.Metadata;
using MassTransit.Serialization;
using NServiceBus.Faults;
using NsbHeaders = NServiceBus.Headers;
using MessageContext = NServiceBus.Transport.MessageContext;

public class MassTransitConverter
{
  public void To(MessageContext messageContext)
  {
    var headers = messageContext.Headers;

    foreach (var key in headers.Keys)
    {
      if (key.StartsWith("NServiceBus."))
      {
        headers.Remove(key);
      }

      if (key.StartsWith("ServiceControl."))
      {
        headers.Remove(key);
      }
    }
  }

  public void From(MessageContext messageContext)
  {
    var headers = messageContext.Headers;

    // TODO: Null/empty checks


    // Could check and validate if envelope is returned, if not, try getting values from headers instead

    // https://masstransit.io/documentation/configuration/serialization

    if (!headers.TryGetValue(MassTransit.MessageHeaders.Reason, out var reason) || reason != "fault")
    {
      throw new InvalidOperationException("Can only forward MassTransit failures");
    }

    // Because transport native content type value isn't provided by transport we probe the headers instead....
    var hasEnvelop = !headers.ContainsKey(MassTransit.MessageHeaders.MessageId);

    var contentType = hasEnvelop
      ? SystemTextJsonMessageSerializer.JsonContentType.ToString() //"application/vnd.masstransit+json"
      : ContentTypes.Json;

    headers[NsbHeaders.ContentType] = contentType;

    if (hasEnvelop)
    {
      var messageEnvelope = DeserializeEnvelope(messageContext);

      // MessageEnvelope
      headers[NsbHeaders.MessageId] = messageEnvelope.MessageId;
      headers[NsbHeaders.EnclosedMessageTypes] = string.Join(",", messageEnvelope.MessageType!);
      headers[NsbHeaders.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(messageEnvelope.SentTime!.Value);
      headers[NsbHeaders.ConversationId] = messageEnvelope.ConversationId;
      if (messageEnvelope.CorrelationId != null)
      {
        headers[NsbHeaders.CorrelationId] = messageEnvelope.CorrelationId;
      }

      if (messageEnvelope.ExpirationTime.HasValue)
      {
        var expirationTime = messageEnvelope.ExpirationTime.Value;
        headers[NsbHeaders.TimeToBeReceived] = DateTimeOffsetHelper.ToWireFormattedString(expirationTime);
      }

      headers[NsbHeaders.OriginatingEndpoint] = messageEnvelope.SourceAddress;
      headers[NsbHeaders.OriginatingMachine] = messageEnvelope.Host!.MachineName;
    }
    else // Get data from headers
    {
      // MessageEnvelope
      headers[NsbHeaders.MessageId] = headers[MassTransit.MessageHeaders.MessageId];
      headers[NsbHeaders.EnclosedMessageTypes] = headers[MassTransit.MessageHeaders.MessageType];
      if (headers.TryGetValue(MassTransit.MessageHeaders.TransportSentTime, out var sent))
      {
        headers[NsbHeaders.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(sent));
      }
      else
      {
        // Using time of failure as fallback
        var faultTimestamp = headers[MassTransit.MessageHeaders.FaultTimestamp];
        headers[NsbHeaders.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(faultTimestamp));
      }

      headers[NsbHeaders.ConversationId] = headers[MassTransit.MessageHeaders.ConversationId];
      if (headers.TryGetValue(MassTransit.MessageHeaders.CorrelationId, out var correlationId))
      {
        headers[NsbHeaders.CorrelationId] = correlationId;
      }

      // if (headers.TryGetValue("TimeToLive", out var ttl))
      // {
      //   headers[NsbHeaders.TimeToBeReceived] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(ttl));
      // }

      headers[NsbHeaders.OriginatingEndpoint] = headers[MassTransit.MessageHeaders.SourceAddress];


      var hostInfo = headers[MassTransit.MessageHeaders.Host.Info];

      var busHostInfo = JsonSerializer.Deserialize<BusHostInfo>(hostInfo) ?? throw new InvalidOperationException();
      headers[NsbHeaders.OriginatingMachine] = busHostInfo.MachineName;
    }

    // MT-Fault-***
    headers[NsbHeaders.DelayedRetries] = headers[MassTransit.MessageHeaders.FaultRetryCount];
    headers[NsbHeaders.ProcessingEndpoint] = headers[MassTransit.MessageHeaders.FaultInputAddress];
    headers[FaultsHeaderKeys.FailedQ] = headers[MassTransit.MessageHeaders.FaultInputAddress];
    headers[FaultsHeaderKeys.ExceptionType] = headers[MassTransit.MessageHeaders.FaultExceptionType];
    headers[FaultsHeaderKeys.Message] = headers[MassTransit.MessageHeaders.FaultMessage];
    headers[FaultsHeaderKeys.StackTrace] = headers[MassTransit.MessageHeaders.FaultStackTrace];
    headers[FaultsHeaderKeys.TimeOfFailure] = headers[MassTransit.MessageHeaders.FaultTimestamp];

    // TODO: Currently using FaultConsumerType as fallback as MT does not have a processing machine equivalent

    headers[NsbHeaders.ProcessingMachine] = headers.GetValueOrDefault(MassTransit.MessageHeaders.Host.MachineName, "❌");
  }

  static MessageEnvelope DeserializeEnvelope(MessageContext messageContext)
  {
    var options = new JsonSerializerOptions
    {
      AllowTrailingCommas = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      ReadCommentHandling = JsonCommentHandling.Skip,
      WriteIndented = true,
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    void Item(JsonTypeInfo typeInfo)
    {
      if (typeInfo.Type == typeof(HostInfo))
      {
        typeInfo.CreateObject = () => new BusHostInfo();
      }
    }

    options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Item } };

    return JsonSerializer.Deserialize<JsonMessageEnvelope>(messageContext.Body.Span, options)
           ?? throw new InvalidOperationException();
  }
}
