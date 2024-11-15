using Amazon.SQS.Model;
using NServiceBus.Transport;

class CustomSqsReceiver(IMessageReceiver receiver) : IMessageReceiver
{
    public Task Initialize(
        PushRuntimeSettings limitations,
        OnMessage onMessage,
        OnError onError,
        CancellationToken cancellationToken = new()
    ) =>
        receiver.Initialize(limitations, (context, ct) =>
        {
            AddNativeHeaders(context);
            return onMessage(context, ct);
        }, onError, cancellationToken);

    void AddNativeHeaders(MessageContext messageContext)
    {
        var nativeMessage = messageContext.Extensions.Get<Message>();
        var headers = messageContext.Headers;

        foreach (var attribute in nativeMessage.MessageAttributes)
        {
            AddIfNotExistThrowIfValueMismatch(messageContext, headers, attribute);
        }
    }

    static void AddIfNotExistThrowIfValueMismatch(MessageContext messageContext, Dictionary<string, string> headers, KeyValuePair<string, MessageAttributeValue> attribute)
    {
        if (headers.TryGetValue(attribute.Key, out var existingHeaderValue))
        {
            if (existingHeaderValue != attribute.Value.StringValue)
            {
                throw new Exception("NServiceBus SQS message header value is different than the native message attribute");
            }
        }
        else
        {
            messageContext.Headers.Add(attribute.Key, attribute.Value.StringValue);
        }
    }

    public Task StartReceive(CancellationToken cancellationToken = default) => receiver.StartReceive(cancellationToken);
    public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default) => receiver.ChangeConcurrency(limitations, cancellationToken);
    public Task StopReceive(CancellationToken cancellationToken = default) => receiver.StopReceive(cancellationToken);
    public ISubscriptionManager Subscriptions => receiver.Subscriptions;
    public string Id => receiver.Id;
    public string ReceiveAddress => receiver.ReceiveAddress;
}