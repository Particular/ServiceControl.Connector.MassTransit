using Azure.Messaging.ServiceBus.Administration;

class AzureServiceBusHelper(string connectionString) : IQueueInformationProvider
{
  private readonly ServiceBusAdministrationClient client = new(connectionString);

  public async Task<IEnumerable<string>> GetQueues()
  {
    var list = new List<string>();
    var result = client.GetQueuesAsync();

    await foreach (var queueProperties in result)
    {
      if (queueProperties.RequiresSession)
      {
        Console.WriteLine("Skipping '{0}', Queues that require sessions are currently unsupported", queueProperties.Name);
        continue;
      }

      list.Add(queueProperties.Name);
    }
    return list;
  }
}
