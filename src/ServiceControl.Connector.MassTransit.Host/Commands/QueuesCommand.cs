namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class QueuesCommand : Command
{
    public QueuesCommand() : base("queues-list", "List queues")
    {
        var filterOption = new Option<string>("--filter", () => ".*_error$", "Use a regex to filter queues by name.");
        AddOption(filterOption);

        this.SetHandler(async context =>
        {
            var filter = context.ParseResult.GetValueForOption(filterOption);

            context.ExitCode = await InternalHandler(filter!, context.GetCancellationToken());
        });
    }

    async Task<int> InternalHandler(string filter, CancellationToken cancellationToken)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddEnvironmentVariables();
        builder.UseMassTransitConnector(true);

        var host = builder.Build();

        var queueInformationProvider = host.Services.GetRequiredService<IQueueInformationProvider>();
        var queues = queueInformationProvider.GetQueues(cancellationToken);

        await foreach (string queue in queues)
        {
            var regEx = new Regex(filter);
            if (regEx.IsMatch(queue))
            {
                Console.WriteLine(queue);
            }
        }

        return 0;
    }
}