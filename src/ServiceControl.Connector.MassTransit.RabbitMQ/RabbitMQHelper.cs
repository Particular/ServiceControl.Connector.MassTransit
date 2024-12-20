using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

class RabbitMQHelper(string vhost, Uri apiBaseUrl, ICredentials credentials) : IQueueInformationProvider
{
    readonly HttpClient httpClient = new(new SocketsHttpHandler
    {
        Credentials = credentials,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    { BaseAddress = apiBaseUrl };

    public async IAsyncEnumerable<string> GetQueues([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var page = 1;
        bool morePages;

        do
        {
            (var queues, morePages) = await GetPage(page, cancellationToken);
            foreach (var name in queues)
            {
                yield return name;
            }
            page++;
        } while (morePages);
    }

    async Task<(string[], bool morePages)> GetPage(int page, CancellationToken cancellationToken)
    {
        var url = $"/api/queues/{HttpUtility.UrlEncode(vhost)}?page={page}&page_size=500&name=&use_regex=false&pagination=true";

        var container = await httpClient.GetFromJsonAsync<JsonNode>(url, cancellationToken);
        switch (container)
        {
            case JsonObject obj:
                {
                    var pageCount = obj["page_count"]!.GetValue<int>();
                    var pageReturned = obj["page"]!.GetValue<int>();

                    if (obj["items"] is not JsonArray items)
                    {
                        return ([], false);
                    }

                    return (MaterializeQueueDetails(items), pageCount > pageReturned);
                }
            // Older versions of RabbitMQ API did not have paging and returned the array of items directly
            case JsonArray arr:
                {
                    return (MaterializeQueueDetails(arr), false);
                }
            default:
                throw new Exception("Was not able to get list of queues from RabbitMQ broker.");
        }
    }

    static string[] MaterializeQueueDetails(JsonArray items)
    {
        // It is not possible to directly operated on the JsonNode. When the JsonNode is a JObject
        // and the indexer is access the internal dictionary is initialized which can cause key not found exceptions
        // when the payload contains the same key multiple times (which happened in the past).
        var queues = items.Select(item => item.Deserialize<JsonElement>().GetProperty("name").GetString()).Where(name => name is not null).ToArray();
        return queues;
    }

    public async Task<bool> QueueExists(string queueName, CancellationToken cancellationToken)
    {
        var url = $"{apiBaseUrl}api/queues/{HttpUtility.UrlEncode(vhost)}/{HttpUtility.UrlEncode(queueName)}";
        // /api/queues/vhost/name
        using var response = await httpClient.GetAsync(url, cancellationToken);
        return response.StatusCode == HttpStatusCode.OK;
    }
}