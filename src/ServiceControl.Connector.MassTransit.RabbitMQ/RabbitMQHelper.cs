using System.Text;
using System.Text.Json;

class RabbitMQHelper : IQueueInformationProvider, IQueueLengthProvider
{
    readonly string _url;
    readonly HttpClient _httpClient;
    readonly string _vhost;

    public RabbitMQHelper(string vhost, Uri apiBaseUrl)
    {
        _vhost = vhost;
        _url = apiBaseUrl + "api/queues";

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

    public async Task<long> GetQueueLength(string name, CancellationToken cancellationToken = default)
    {
        var requestUri = $"{_url}/{_vhost.Replace("/", "%2F")}/{name}"; //Encode /
        try
        {
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var jsonDoc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

            if (jsonDoc.RootElement.TryGetProperty("messages", out var value))
            {
                return value.GetInt64();
            }

            return 0; //No value available yet shortly after queue has been created
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //Shutting down
            return 0;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to check the length of the queue {name} via URL {requestUri}.", e);
        }
    }
}