namespace ServiceControl.Connector.MassTransit.Tests;

using NUnit.Framework;

[TestFixture]
public class HeartbeatMessageSizeReducerTests
{
    [Test]
    public void Should_not_reduce_if_within_limits()
    {
        var largeString = new string('a', 100);
        var originalHeartbeat = new MassTransitConnectorHeartbeat
        {
            SentDateTimeOffset = DateTimeOffset.UtcNow,
            Version = ConnectorVersion.Version,
            Logs = Enumerable.Range(0, 100).Select(_ => new LogEntry(DateTimeOffset.UtcNow, "Error", largeString)).ToArray(),
            ErrorQueues = Enumerable.Range(0, 100).Select(i => new ErrorQueue { Name = $"my_super_queue_{i}", Ingesting = false }).ToArray()
        };

        var heartbeat = new Heartbeat.HeartbeatMessageSizeReducer(originalHeartbeat).Reduce();

        Assert.That(heartbeat.Logs.Length, Is.EqualTo(100));
        Assert.That(heartbeat.ErrorQueues.Length, Is.EqualTo(100));
    }

    [Test]
    public void Should_reduce_logs_first()
    {
        var largeString = new string('a', 4000);
        var originalHeartbeat = new MassTransitConnectorHeartbeat
        {
            SentDateTimeOffset = DateTimeOffset.UtcNow,
            Version = ConnectorVersion.Version,
            Logs = Enumerable.Range(0, 100).Select(_ => new LogEntry(DateTimeOffset.UtcNow, "Error", largeString)).ToArray(),
            ErrorQueues = Enumerable.Range(0, 2000).Select(i => new ErrorQueue { Name = $"my_super_queue_{i}", Ingesting = false }).ToArray()
        };

        var heartbeat = new Heartbeat.HeartbeatMessageSizeReducer(originalHeartbeat).Reduce();

        Assert.That(heartbeat.Logs.Length, Is.LessThan(100).And.GreaterThanOrEqualTo(10));
        Assert.That(heartbeat.ErrorQueues.Length, Is.EqualTo(2000));
    }

    [Test]
    public void Should_reduce_queues_after_reducing_logs()
    {
        var largeString = new string('a', 400000);
        var originalHeartbeat = new MassTransitConnectorHeartbeat
        {
            SentDateTimeOffset = DateTimeOffset.UtcNow,
            Version = ConnectorVersion.Version,
            Logs = Enumerable.Range(0, 100).Select(_ => new LogEntry(DateTimeOffset.UtcNow, "Error", largeString)).ToArray(),
            ErrorQueues = Enumerable.Range(0, 5000).Select(i => new ErrorQueue { Name = $"my_super_queue_{i}", Ingesting = false }).ToArray()
        };

        var heartbeat = new Heartbeat.HeartbeatMessageSizeReducer(originalHeartbeat).Reduce();

        Assert.That(heartbeat.Logs.Length, Is.EqualTo(0));
        Assert.That(heartbeat.ErrorQueues.Length, Is.LessThan(5000).And.GreaterThanOrEqualTo(1000));
    }
}