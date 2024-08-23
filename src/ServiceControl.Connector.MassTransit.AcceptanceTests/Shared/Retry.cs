using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
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
            .WithMassTransit("Receiver", bus => { bus.AddConsumer<FaultyConsumer>(); })
            .WithMassTransit("Sender", bus => { }, (context, collection) =>
            {
                collection.AddHostedService<Sender>();
            })
            .WithEndpoint<ErrorSpy>()
            .Done(c => c.MessageProcessed)
            .Run();

        Assert.That(ctx.MessageFailed, Is.True);
        Assert.That(ctx.MessageProcessed, Is.True);
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
                await bus.Publish(new FaultyMessage(), cancellationToken);
            }
        }
    }

    public class FaultyConsumer : IConsumer<FaultyMessage>
    {
        readonly Context testContext;

        public FaultyConsumer(Context testContext)
        {
            this.testContext = testContext;
        }

        public Task Consume(ConsumeContext<FaultyMessage> context)
        {
            testContext.FirstMessageReceived = true;
            if (!testContext.MessageFailed)
            {
                throw new Exception("Simulated");
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
            readonly Context testContext;

            public ReturnBehavior(IMessageDispatcher dispatcher, Context testContext)
            {
                this.dispatcher = dispatcher;
                this.testContext = testContext;
            }

            public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                testContext.MessageFailed = true;
                var headers = new Dictionary<string, string>(context.Message.Headers);

                //Simulate ServiceControl retry behavior
                var failedQueue = context.Message.Headers["NServiceBus.FailedQ"];
                headers["ServiceControl.TargetEndpointAddress"] = failedQueue;

                var returnMessage = new OutgoingMessage(context.MessageId, headers, context.Message.Body);

                var transportOperation = new TransportOperation(returnMessage, new UnicastAddressTag("Retry.Return"));
                return dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction());
            }
        }
    }

    public class Context : ScenarioContext
    {
        public bool MessageFailed { get; set; }
        public bool MessageProcessed { get; set; }
        public bool FirstMessageReceived { get; set; }
    }
}

namespace RetryTest
{
    public class FaultyMessage
    {

    }
}