using NUnit.Framework;

public class RetryTests : ConnectorAcceptanceTest
{
    [Test]
    public async Task Should_forward_error_messages_by_not_modify_message()
    {
        await new Retry().Should_forward_error_messages_by_not_modify_message("failing_error");
    }
}