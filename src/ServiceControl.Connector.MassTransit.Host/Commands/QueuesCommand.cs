namespace ServiceControl.Connector.MassTransit.Host.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class QueuesCommand : Command
{
    public QueuesCommand() : base("queues-list", "List queues")
    {
        var filterOption = new Option<string>("--filter")
        {
            DefaultValueFactory = _ => ".*_error$",
            Description = "Use a regex to filter queues by name.",
        };
        Add(filterOption);

        this.AddConnectorOptions();

        SetAction(async (parseResult, cancellationToken) =>
        {
            var filter = parseResult.GetValue(filterOption);
            var connectorArgs = ConnectorCommandOptions.BuildArgs(parseResult);

            return await InternalHandler(filter!, connectorArgs, cancellationToken);
        });
    }

    async Task<int> InternalHandler(string filter, string[] connectorArgs, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder(connectorArgs);
        builder.UseMassTransitConnector(true);

        using var host = builder.Build();

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