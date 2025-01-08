using System.Net;
using System.Text.Json;

class RabbitMQHelper : IQueueInformationProvider
{
    readonly string _url;
    readonly HttpClient _httpClient;
    readonly string _vhost;

    public RabbitMQHelper(string vhost, Uri apiBaseUrl, ICredentials credentials)
    {
        _vhost = vhost;
        _url = apiBaseUrl + "api/queues";

        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            Credentials = credentials,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        { BaseAddress = apiBaseUrl };
    }

    public async Task<IEnumerable<string>> GetQueues(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(_url, cancellationToken);

        response.EnsureSuccessStatusCode();

        var jsonText = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(jsonText);

        var list = new List<string>();

        foreach (var x in jsonDoc.RootElement.EnumerateArray())
        {
            var queueName = x.GetProperty("name").GetString() ?? throw new("no name");
            var queueVHost = x.GetProperty("vhost").GetString() ?? throw new("no vhost");

            if (_vhost == queueVHost)
            {
                list.Add(queueName);
            }
        }

        return list;
    }
}