using System.Text;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Features;
using NServiceBus.Pipeline;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.Connector.MassTransit;
using ServiceControl.Plugin.CustomChecks.Messages;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class ForwardingFails
{
    public async Task Should_forward_error_messages_by_not_modify_message(string queueName)
    {
        var ctx = await Scenario.Define<Context>()
            .WithConnector("Connector", Conventions.EndpointNamingConvention(typeof(ErrorSpy)), "Retry.Return", [queueName], Conventions.EndpointNamingConvention(typeof(CustomCheckSpy)))
            .WithEndpoint<InvalidMessageSender>()
            .WithEndpoint<CustomCheckSpy>()
            .Done(c => c.Failed)
            .Run();

        Assert.That(ctx.Failed, Is.True);
        Assert.That(ctx.FailureReason, Does.StartWith("Queue `Retry.Return.poison` has"));
    }

    public class InvalidMessageSender : EndpointConfigurationBuilder
    {
        public InvalidMessageSender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.SendFailedMessagesTo("InvalidMessageSender_error"); //Ensures this queue gets created
                c.RegisterStartupTask<InvalidMessageSenderTask>();
            });
        }

        class InvalidMessageSenderTask : FeatureStartupTask
        {
            readonly IMessageDispatcher messageDispatcher;

            public InvalidMessageSenderTask(IMessageDispatcher messageDispatcher)
            {
                this.messageDispatcher = messageDispatcher;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                var dictionary = new Dictionary<string, string>
                {
                    ["CustomHeader"] = "CustomValue"
                };
                var body = Encoding.Latin1.GetBytes("Plain text encoded in weird way");
                var outgoingMessage = new OutgoingMessage(Guid.NewGuid().ToString(), dictionary, body);
                var transportOperation = new TransportOperation(outgoingMessage, new UnicastAddressTag("InvalidMessageSender_error"));
                return messageDispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(),
                    cancellationToken);
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                return Task.CompletedTask;
            }
        }
    }

    public class CustomCheckSpy : EndpointConfigurationBuilder
    {
        public CustomCheckSpy()
        {
            EndpointSetup<DefaultServer>(c =>
            {
            });
        }

        class CustomCheckHandler : IHandleMessages<ReportCustomCheckResult>
        {
            Context scenarioContext;

            public CustomCheckHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(ReportCustomCheckResult message, IMessageHandlerContext context)
            {
                scenarioContext.FailureReason = message.FailureReason;
                scenarioContext.Failed = message.HasFailed;
                return Task.CompletedTask;
            }
        }

        class MassTransitConnectorHeartbeatHandler : IHandleMessages<MassTransitConnectorHeartbeat>
        {
            public Task Handle(MassTransitConnectorHeartbeat message, IMessageHandlerContext context)
            {
                return Task.CompletedTask;
            }
        }
    }

    public class ErrorSpy : EndpointConfigurationBuilder
    {
        public ErrorSpy()
        {
            var endpoint = EndpointSetup<DefaultServer>(c =>
            {
                c.Pipeline.Register(typeof(ReturnBehavior), "Returns the message to the source queue");
            });
        }

        class ReturnBehavior : Behavior<IIncomingPhysicalMessageContext>
        {
            Context scenarioContext;

            public ReturnBehavior(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                //This should never happen
                scenarioContext.ForwardedToServiceControl = true;
                return Task.CompletedTask;
            }
        }
    }

    public class Context : ScenarioContext
    {
        public bool ForwardedToServiceControl { get; set; }
        public string? FailureReason { get; set; }
        public bool Failed { get; set; }
    }
}

namespace ServiceControl.Plugin.CustomChecks.Messages
{
    using System;

#pragma warning disable CS8618
    public class ReportCustomCheckResult
    {
        public Guid HostId { get; set; }
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }
        public DateTime ReportedAt { get; set; }
        public string EndpointName { get; set; }
        public string Host { get; set; }
    }
#pragma warning restore CS8618
}

