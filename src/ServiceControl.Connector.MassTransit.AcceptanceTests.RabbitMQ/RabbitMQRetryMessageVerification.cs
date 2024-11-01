using MassTransit;
using NUnit.Framework;
using RetryTest;

class RabbitMQRetryMessageVerification : IRetryMessageVerification
{
    public void Verify(ConsumeContext<FaultyMessage> context)
    {
        Assert.That(context.ReceiveContext.ContentType.ToString(), Is.EqualTo("application/vnd.masstransit+json"));
    }
}