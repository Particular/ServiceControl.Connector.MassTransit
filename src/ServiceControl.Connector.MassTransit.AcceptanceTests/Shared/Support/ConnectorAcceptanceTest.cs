﻿using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

public class ConnectorAcceptanceTest
{
    [SetUp]
    public void SetUp()
    {
        NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention = t =>
        {
            if (string.IsNullOrWhiteSpace(t.FullName))
            {
                throw new InvalidOperationException($"The type {nameof(t)} has no fullname to work with.");
            }

            var classAndEndpoint = t.FullName.Split('.').Last();

            var testName = classAndEndpoint.Split('+').First();

            var endpointBuilder = classAndEndpoint.Split('+').Last();

            testName = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);

            testName = testName.Replace("_", "");

            return testName + "." + endpointBuilder;
        };
    }

    [TearDown]
    public async Task TearDown()
    {
        await TestSuiteConfiguration.Current.Cleanup();

        if (!TestExecutionContext.CurrentContext.TryGetRunDescriptor(out var runDescriptor))
        {
            return;
        }

        var scenarioContext = runDescriptor.ScenarioContext;

        if (Environment.GetEnvironmentVariable("CI") != "true" || Environment.GetEnvironmentVariable("VERBOSE_TEST_LOGGING")?.ToLower() == "true")
        {
#pragma warning disable NUnit1033
            TestContext.WriteLine($@"Test settings:
{string.Join(Environment.NewLine, runDescriptor.Settings.Select(setting => $"   {setting.Key}: {setting.Value}"))}");

            TestContext.WriteLine($@"Context:
{string.Join(Environment.NewLine, scenarioContext.GetType().GetProperties().Select(p => $"{p.Name} = {p.GetValue(scenarioContext, null)}"))}");
        }

        if (TestExecutionContext.CurrentContext.CurrentResult.ResultState == ResultState.Failure || TestExecutionContext.CurrentContext.CurrentResult.ResultState == ResultState.Error)
        {
            TestContext.WriteLine(string.Empty);
            TestContext.WriteLine($"Log entries (log level: {scenarioContext.LogLevel}):");
            TestContext.WriteLine("--- Start log entries ---------------------------------------------------");
            foreach (var logEntry in scenarioContext.Logs)
            {
                TestContext.WriteLine($"{logEntry.Timestamp:HH:mm:ss:ffff} {logEntry.Level} {logEntry.Endpoint ?? TestContext.CurrentContext.Test.Name}: {logEntry.Message}");
            }
            TestContext.WriteLine("--- End log entries ---------------------------------------------------");
#pragma warning restore NUnit1033
        }
    }
}