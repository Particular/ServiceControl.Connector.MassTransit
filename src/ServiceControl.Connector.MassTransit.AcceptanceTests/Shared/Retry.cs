using System.Collections.Concurrent;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Pipeline;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NUnit.Framework;
using RetryTest;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class Retry : ConnectorAcceptanceTest
{
    [Test]
    public async Task Should_forward_error_messages_by_not_modify_message()
    {
        var ctx = await Scenario.Define<Context>()
            .WithConnector("Connector", Conventions.EndpointNamingConvention(typeof(ErrorSpy)), "Retry.Return")
            .WithMassTransit("Receiver", bus =>
            {
                bus.AddConsumer<FailingConsumer>();
            })
            .WithMassTransit("Sender", bus => { }, (context, collection) =>
            {
                collection.AddHostedService<Sender>();
            })
            .WithEndpoint<ErrorSpy>()
            .Done(c => c.MessageProcessed)
            .Run();

        Assert.That(ctx.MessageProcessed, Is.True);
        ctx.VerifyMessageAction?.Invoke();
    }

    public class Sender : BackgroundService
    {
        readonly IBus bus;
        readonly Context testContext;

        public Sender(IBus bus, Context testContext)
        {
            this.bus = bus;
            this.testContext = testContext;
        }

#pragma warning disable PS0003
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
#pragma warning restore PS0003
        {
            while (!testContext.FirstMessageReceived && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                await bus.Publish(new FaultyMessage(), cancellationToken);
            }
        }
    }

    public class FailingConsumer : IConsumer<FaultyMessage>
    {
        readonly Context testContext;
        readonly IServiceProvider serviceProvider;

        public FailingConsumer(Context testContext, IServiceProvider serviceProvider)
        {
            this.testContext = testContext;
            this.serviceProvider = serviceProvider;
        }

        public Task Consume(ConsumeContext<FaultyMessage> context)
        {
            testContext.FirstMessageReceived = true;
            if (!testContext.MessageStatus.TryGetValue(context.MessageId!.Value, out var failed))
            {
                testContext.MessageStatus.TryAdd(context.MessageId.Value, true);
                throw new Exception("Simulated");
            }

            var verification = serviceProvider.GetService<IRetryMessageVerification>();
            if (verification != null)
            {
                testContext.VerifyMessageAction = () => verification.Verify(context);
            }
            testContext.MessageProcessed = true;
            return Task.CompletedTask;
        }
    }

    public class ErrorSpy : EndpointConfigurationBuilder
    {
        public ErrorSpy()
        {
            var endpoint = EndpointSetup<DefaultServer>(c =>
            {
                c.AutoSubscribe().DisableFor<FaultyMessage>();
                c.Pipeline.Register(typeof(ReturnBehavior), "Returns the message to the source queue");
            });
        }

        class ReturnBehavior : Behavior<IIncomingPhysicalMessageContext>
        {
            readonly IMessageDispatcher dispatcher;

            public ReturnBehavior(IMessageDispatcher dispatcher)
            {
                this.dispatcher = dispatcher;
            }

            public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                var headers = new Dictionary<string, string>(context.Message.Headers);

                //Simulate ServiceControl retry behavior
                var failedQueue = context.Message.Headers["NServiceBus.FailedQ"];
                headers["ServiceControl.TargetEndpointAddress"] = failedQueue;
                headers["ServiceControl.Retry.AcknowledgementQueue"] = Conventions.EndpointNamingConvention(typeof(ErrorSpy));

                var returnMessage = new OutgoingMessage(context.MessageId, headers, context.Message.Body);

                var transportOperation = new TransportOperation(returnMessage, new UnicastAddressTag("Retry.Return"));
                return dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction());
            }
        }
    }

    public class Context : ScenarioContext
    {
        public ConcurrentDictionary<Guid, bool> MessageStatus { get; set; } = new ();
        public bool MessageProcessed { get; set; }
        public bool FirstMessageReceived { get; set; }
        public Action? VerifyMessageAction { get; set; }

    }
}

public interface IRetryMessageVerification
{
    void Verify(ConsumeContext<FaultyMessage> context);
}

namespace RetryTest
{
    public class FaultyMessage
    {
    }
}