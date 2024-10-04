using System.Text;
using System.Text.Json;

class RabbitMQHelper : IQueueInformationProvider
{
    readonly string _url;
    readonly HttpClient _httpClient;
    readonly string _vhost;

    public RabbitMQHelper(string vhost, Uri apiBaseUrl)
    {
        _vhost = vhost;
        _url = apiBaseUrl + "api/queues";
        Console.WriteLine($"RabbitMQ API URL: {_url}");
        Console.WriteLine($"ApiBase {apiBaseUrl.UserInfo}");
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(apiBaseUrl.UserInfo));

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
    }
#pragma warning disable PS0018
    public async Task<IEnumerable<string>> GetQueues()
#pragma warning restore PS0018
    {
        var response = await _httpClient.GetAsync(_url);

        response.EnsureSuccessStatusCode();

        var jsonText = await response.Content.ReadAsStringAsync();
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