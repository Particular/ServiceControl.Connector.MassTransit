using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MassTransit;
using MassTransit.Metadata;
using MassTransit.Serialization;
using Microsoft.Extensions.Logging;
using NServiceBus.Faults;
using NsbHeaders = NServiceBus.Headers;
using MessageContext = NServiceBus.Transport.MessageContext;
using ServiceControl.Adapter.MassTransit;

public class MassTransitConverter(ILogger<MassTransitConverter> logger)
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
        }
    }

    public void From(MessageContext messageContext)
    {
        var headers = messageContext.Headers;

        // TODO: Null/empty checks


        // Could check and validate if envelope is returned, if not, try getting values from headers instead

        // https://masstransit.io/documentation/configuration/serialization

        if (!headers.TryGetValue(MessageHeaders.Reason, out var reason) || reason != "fault")
        {
            throw new InvalidOperationException("Can only forward MassTransit failures");
        }

        // Because transport native content type value isn't provided by transport we probe the headers instead....
        var hasEnvelope = !headers.ContainsKey(MessageHeaders.MessageId);

        var contentType = hasEnvelope
            ? SystemTextJsonMessageSerializer.JsonContentType.ToString() //"application/vnd.masstransit+json"
            : ContentTypes.Json;

        headers[NsbHeaders.ContentType] = contentType;

#pragma warning disable PS0022
        if (hasEnvelope)
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
            if (messageEnvelope.Host?.MachineName != null)
            {
                headers[NsbHeaders.OriginatingMachine] = messageEnvelope.Host?.MachineName;
            }
        }
        else // Get data from headers
        {
            // MessageEnvelope
            headers[NsbHeaders.MessageId] = headers[MessageHeaders.MessageId];
            headers[NsbHeaders.EnclosedMessageTypes] = headers[MessageHeaders.MessageType];
            if (headers.TryGetValue(MessageHeaders.TransportSentTime, out var sent))
            {
                headers[NsbHeaders.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(sent));
            }
            else
            {
                logger.LogWarning($"Using {MessageHeaders.FaultTimestamp} as fallback for missing {MessageHeaders.TransportSentTime}");
                // Using time of failure as fallback
                var faultTimestampFallback = headers[MessageHeaders.FaultTimestamp];
                headers[NsbHeaders.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(faultTimestampFallback));
            }

            headers[NsbHeaders.ConversationId] = headers[MessageHeaders.ConversationId];
            if (headers.TryGetValue(MessageHeaders.CorrelationId, out var correlationId))
            {
                headers[NsbHeaders.CorrelationId] = correlationId;
            }

            // if (headers.TryGetValue("TimeToLive", out var ttl))
            // {
            //   headers[NsbHeaders.TimeToBeReceived] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(ttl));
            // }

            headers[NsbHeaders.OriginatingEndpoint] = headers[MessageHeaders.SourceAddress];


            var hostInfo = headers[MessageHeaders.Host.Info];

            var busHostInfo = JsonSerializer.Deserialize<BusHostInfo>(hostInfo) ??
                              throw new InvalidOperationException();
            headers[NsbHeaders.OriginatingMachine] = busHostInfo.MachineName;
        }
        headers[NsbHeaders.HostId] = DeterministicGuid.MakeId(headers[NsbHeaders.OriginatingEndpoint], headers[MessageHeaders.Host.MachineName]).ToString("N");
        headers["NServiceBus.ConnectedApplication"] = Heartbeat.ConnectedApplicationName;


        // MT-Fault-***
        if (headers.TryGetValue(MessageHeaders.FaultRetryCount, out var faultRetryCount))
        {
            headers[NsbHeaders.DelayedRetries] = faultRetryCount;
        }

        if (headers.TryGetValue(MessageHeaders.FaultInputAddress, out var faultInputAddress))
        {
            headers[NsbHeaders.ProcessingEndpoint] = faultInputAddress;
            headers[FaultsHeaderKeys.FailedQ] = faultInputAddress;
        }
        else
        {
            // TODO: Getting the fault address this way should NOT be an alternate flow but be a the primary based on some setting
            // If this is a DLQ the input address is inferred from the queue it self
            faultInputAddress = messageContext.ReceiveAddress;
            logger.LogWarning($"Using `messageContext.ReceiveAddress` for {NsbHeaders.ProcessingEndpoint} as fallback for missing {MessageHeaders.FaultInputAddress}");
            headers[NsbHeaders.ProcessingEndpoint] = faultInputAddress;
            logger.LogWarning($"Using `messageContext.ReceiveAddress` for {FaultsHeaderKeys.FailedQ} as fallback for missing {MessageHeaders.FaultInputAddress}");
            headers[MessageHeaders.FaultInputAddress] = "queue:" + faultInputAddress;
            headers[FaultsHeaderKeys.FailedQ] = faultInputAddress;
        }

        if (headers.TryGetValue(MessageHeaders.FaultExceptionType, out var faultExceptionType))
        {
            headers[FaultsHeaderKeys.ExceptionType] = faultExceptionType;
        }
        else
        {
            //TODO: ServiceControl shows ": 0" when ExceptionType and ExceptionStackTrace
        }

        headers[FaultsHeaderKeys.Message] = headers[MessageHeaders.FaultMessage];

        if (headers.TryGetValue(MessageHeaders.FaultStackTrace, out var faultStackTrace))
        {
            headers[FaultsHeaderKeys.StackTrace] = faultStackTrace;
        }
        else
        {
            logger.LogInformation("Message does not have {HeaderKey}", MessageHeaders.FaultStackTrace);
        }

        if (headers.TryGetValue(MessageHeaders.FaultTimestamp, out var faultTimestamp))
        {
            headers[FaultsHeaderKeys.TimeOfFailure] = DateTimeOffsetHelper.ToWireFormattedString(DateTime.Parse(faultTimestamp));
        }
        else
        {
            // TODO: Not sure on this, maybe there is a better source
            logger.LogWarning($"Using current time for {FaultsHeaderKeys.TimeOfFailure} as fallback for missing {MessageHeaders.FaultTimestamp}");
            headers[FaultsHeaderKeys.TimeOfFailure] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow);
        }

        // TODO: Currently using FaultConsumerType as fallback as MT does not have a processing machine equivalent
        headers[NsbHeaders.ProcessingMachine] = headers.GetValueOrDefault(MessageHeaders.Host.MachineName, "❌");
#pragma warning restore PS0018
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