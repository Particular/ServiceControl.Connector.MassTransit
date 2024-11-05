namespace ServiceControl.Connector.MassTransit.Tests;

using NUnit.Framework;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UserProvidedQueueNameFilterTests
{
    [TestCase("ThisIsATest", "^This", ExpectedResult = true)]
    [TestCase("ThisIsATest", "Is", ExpectedResult = true)]
    [TestCase("ThisIsATest", "Test$", ExpectedResult = true)]
    [TestCase("ThisIsATest", "^Test", ExpectedResult = false)]
    [TestCase("ThisIsATest", "Development", ExpectedResult = false)]
    [TestCase("ThisIsATest", "This$", ExpectedResult = false)]
    public bool IsMatch(string value, string regex)
    {
        var filter = new UserProvidedQueueNameFilter(regex);
        return filter.IsMatch(value);
    }
}