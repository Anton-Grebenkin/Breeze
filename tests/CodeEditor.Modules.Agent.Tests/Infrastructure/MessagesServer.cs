using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>
/// Anthropic Messages server for the real client: records the URI, headers and request bodies and replies with the
/// given body, either an event stream (<see cref="Event"/>) or an error.
/// </summary>
internal sealed class MessagesServer(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public Uri? Uri { get; private set; }

    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<JsonObject> Bodies { get; } = [];

    /// <summary>Messages model client as the factory creates it, with this server instead of the network.</summary>
    public IChatClient Client(string model, string endpoint = AgentServices.ProvodEndpoint)
    {
        var options = new AgentOptions { Endpoint = endpoint, Model = model };
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, new Uri(endpoint));
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(this, disposeHandler: false));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        return OpenAIChatClientFactory.Create(options, new ApiKeyCredential("test"), clientOptions, streamRetryDelays: []);
    }

    /// <summary>Messages stream event: the type goes both in the <c>event</c> line and in the data.</summary>
    public static string Event(string type, string data)
    {
        var json = JsonNode.Parse(data)!.AsObject();
        json["type"] = type;
        return $"event: {type}\ndata: {json.ToJsonString()}\n\n";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri = request.RequestUri;
        foreach (var header in request.Headers)
        {
            Headers[header.Key] = string.Join(',', header.Value);
        }

        if (request.Content is not null)
        {
            Bodies.Add(JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject());
        }

        var type = status == HttpStatusCode.OK ? "text/event-stream" : "application/json";
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) };
    }
}
