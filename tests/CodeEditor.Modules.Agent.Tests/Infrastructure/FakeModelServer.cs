using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>
/// Model server for the real OpenAI client: records request bodies and headers and answers each with a stream of
/// scripted output items, in Responses API or Chat Completions format depending on the request path. Items follow the
/// Responses output: reasoning (<see cref="Reasoning"/>), text (<see cref="Message"/>), call (<see cref="Call"/>).
/// When the script runs out, the model answers with a short text.
/// </summary>
internal sealed class FakeModelServer : HttpMessageHandler
{
    private readonly Queue<JsonObject[]> _outputs = new();
    private int _responses;

    public List<string> Bodies { get; } = [];

    public List<Dictionary<string, string>> Headers { get; } = [];

    public void Enqueue(params JsonObject[] items) => _outputs.Enqueue(items);

    /// <summary>Makes the fixture's model a real OpenAI client talking to this server instead of the network.</summary>
    public void Connect(AgentFixture fixture, AgentOptions options)
    {
        fixture.Options.Set(options);
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, new Uri("https://proxy.test/v1"), () => fixture.CacheKey.Current);
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(this, disposeHandler: false));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        fixture.Model = OpenAIChatClientFactory.Create(options, new ApiKeyCredential("test"), clientOptions);
    }

    /// <summary>Request history: Responses <c>input</c> or Chat Completions <c>messages</c>.</summary>
    public JsonArray History(int request)
    {
        var body = JsonNode.Parse(Bodies[request])!;
        return (body["input"] ?? body["messages"])!.AsArray();
    }

    public static JsonObject Reasoning(string id, string summary) => new()
    {
        ["type"] = "reasoning",
        ["id"] = id,
        ["summary"] = new JsonArray(new JsonObject { ["type"] = "summary_text", ["text"] = summary }),
        ["encrypted_content"] = "encrypted-" + id,
    };

    public static JsonObject Message(string id, string text) => new()
    {
        ["type"] = "message",
        ["id"] = id,
        ["role"] = "assistant",
        ["status"] = "completed",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = text, ["annotations"] = new JsonArray() }),
    };

    public static JsonObject Call(string callId, string name, string arguments) => new()
    {
        ["type"] = "function_call",
        ["id"] = "fc_" + callId,
        ["call_id"] = callId,
        ["name"] = name,
        ["arguments"] = arguments,
        ["status"] = "completed",
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        Headers.Add(request.Headers.ToDictionary(header => header.Key, header => string.Join(',', header.Value), StringComparer.OrdinalIgnoreCase));
        var id = "resp_" + ++_responses;
        var items = _outputs.Count > 0 ? _outputs.Dequeue() : [Message("msg_" + id, "Всё.")];
        var stream = request.RequestUri!.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal)
            ? CompletionsStream.Write(id, items)
            : ResponsesStream(id, items);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(stream, Encoding.UTF8, "text/event-stream") };
    }

    // Responses API events: as in a real stream, reasoning and text arrive as deltas before the finished item.
    private static string ResponsesStream(string id, JsonObject[] items)
    {
        var stream = new StringBuilder();
        var sequence = 0;
        void Event(string type, JsonObject data)
        {
            data["type"] = type;
            data["sequence_number"] = sequence++;
            stream.Append("event: ").Append(type).Append('\n').Append("data: ").Append(data.ToJsonString()).Append("\n\n");
        }

        Event("response.created", new JsonObject { ["response"] = Response(id, "in_progress", []) });
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            Event("response.output_item.added", new JsonObject { ["output_index"] = index, ["item"] = item.DeepClone() });
            if (Delta(item) is { } delta)
            {
                Event(delta.Type, new JsonObject { ["item_id"] = item["id"]!.DeepClone(), ["output_index"] = index, ["summary_index"] = 0, ["content_index"] = 0, ["delta"] = delta.Text });
            }

            Event("response.output_item.done", new JsonObject { ["output_index"] = index, ["item"] = item.DeepClone() });
        }

        Event("response.completed", new JsonObject { ["response"] = Response(id, "completed", items) });
        return stream.ToString();
    }

    private static (string Type, string Text)? Delta(JsonObject item) => (string?)item["type"] switch
    {
        "reasoning" => ("response.reasoning_summary_text.delta", (string)item["summary"]![0]!["text"]!),
        "message" => ("response.output_text.delta", (string)item["content"]![0]!["text"]!),
        _ => null,
    };

    private static JsonObject Response(string id, string status, JsonObject[] items) => new()
    {
        ["id"] = id,
        ["object"] = "response",
        ["created_at"] = 1_760_000_000,
        ["status"] = status,
        ["model"] = "model",
        ["output"] = new JsonArray([.. items.Select(item => item.DeepClone())]),
        ["parallel_tool_calls"] = true,
        ["tool_choice"] = "auto",
        ["tools"] = new JsonArray(),
        ["usage"] = new JsonObject
        {
            ["input_tokens"] = 100,
            ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 0 },
            ["output_tokens"] = 10,
            ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 5 },
            ["total_tokens"] = 110,
        },
    };
}
