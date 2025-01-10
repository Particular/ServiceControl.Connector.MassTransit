using NUnit.Framework;

public class ForwardingFailsTests : ConnectorAcceptanceTest
{
    [Test]
    public async Task Should_forward_error_messages_by_not_modify_message()
    {
        await new ForwardingFails().Should_forward_error_messages_by_not_modify_message("invalidmessagesender");
    }
}