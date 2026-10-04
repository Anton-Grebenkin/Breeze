using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Options;
using OpenAI;

namespace CodeEditor.Modules.Agent.Services.Web;

/// <summary>
/// Web search for the <c>web_search</c> tool (ADR 0025): a separate short Chat Completions request to a fast model
/// (<see cref="AgentOptions.WebSearchModel"/>) with the service's server tool <c>aitunnel:web_search</c>. The model
/// searches, reads the results and answers with a digest; sources arrive as <c>url_citation</c> annotations. The main
/// agent request is unchanged, so search works with any model and protocol at the cost of one search and fast-model tokens.
/// </summary>
/// <param name="transport">Transport; a fake server in tests.</param>
public sealed class WebSearchClient(
    ISecretStore secrets, IOptionsMonitor<AgentOptions> options, HelperUsage usage, UserDataPaths paths, TimeProvider time, PipelineTransport? transport = null)
{
    public const string ServerTool = "aitunnel:web_search";

    /// <summary>Search results per query: enough for an answer without bloating the fast model's input.</summary>
    public const int MaxResults = 5;

    private const int MaxOutputTokens = 2_000;

    private const string Instructions =
        "Search the web to answer the query. Reply with the facts you found, concisely, with versions and dates where they matter; " +
        "mark each fact with the source number like [1]. If nothing relevant is found, say so. Write in the language of the query.";

    /// <summary>The configured service supports search.</summary>
    public bool IsAvailable => AgentServices.DialectFor(options.CurrentValue.Endpoint).WebSearchTool;

    /// <exception cref="ClientResultException">The service returned an error.</exception>
    public async Task<WebSearchResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var agent = options.CurrentValue;
        var search = new AgentOptions { Endpoint = agent.Endpoint, Model = agent.WebSearchModel };
        var (key, endpoint) = OpenAIChatClientFactory.Connection(search, secrets);
        var model = AgentServices.DialectFor(search.Endpoint).ShortModelIds ? ModelVendors.ShortName(search.Model) : search.Model;

        var pipeline = Pipeline(key, agent);
        using var message = pipeline.CreateMessage();
        message.Apply(new RequestOptions { CancellationToken = cancellationToken });
        message.Request.Method = "POST";
        message.Request.Uri = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/chat/completions");
        message.Request.Headers.Set("Content-Type", "application/json");
        message.Request.Content = BinaryContent.Create(BinaryData.FromString(Request(model, query).ToJsonString()));
        await pipeline.SendAsync(message);
        var response = message.Response ?? throw new InvalidOperationException("The pipeline returned no response.");
        if (response.IsError)
        {
            throw await ClientResultException.CreateAsync(response);
        }

        using var document = JsonDocument.Parse(response.Content.ToMemory());
        CountUsage(document.RootElement);
        return Parse(document.RootElement);
    }

    /// <summary>Request body: instructions, the query and the server search tool; the answer is a digest of up to 2000 tokens.</summary>
    internal static JsonObject Request(string model, string query) => new()
    {
        ["model"] = model,
        ["messages"] = new JsonArray(
            new JsonObject { ["role"] = "system", ["content"] = Instructions },
            new JsonObject { ["role"] = "user", ["content"] = query }),
        ["tools"] = new JsonArray(new JsonObject { ["type"] = ServerTool, ["max_results"] = MaxResults }),
        ["max_tokens"] = MaxOutputTokens,
    };

    /// <summary>The digest is the answer text; sources are the <c>url_citation</c> annotations, deduplicated, in order.</summary>
    internal static WebSearchResult Parse(JsonElement completion)
    {
        if (!completion.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var message))
        {
            return new WebSearchResult(string.Empty, []);
        }

        var answer = message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String ? content.GetString() ?? string.Empty : string.Empty;
        var sources = new List<WebSource>();
        if (message.TryGetProperty("annotations", out var annotations) && annotations.ValueKind == JsonValueKind.Array)
        {
            foreach (var annotation in annotations.EnumerateArray())
            {
                if (annotation.TryGetProperty("url_citation", out var citation) && Text(citation, "url") is { } url && sources.All(source => source.Url != url))
                {
                    sources.Add(new WebSource(Text(citation, "title") ?? url, url));
                }
            }
        }

        return new WebSearchResult(answer.Trim(), sources);
    }

    // Traffic log as for the agent conversation: the raw search response with annotations is visible with agent.trafficLog.
    private ClientPipeline Pipeline(string key, AgentOptions agent)
    {
        var pipelineOptions = new OpenAIClientOptions();
        pipelineOptions.AddPolicy(new ErrorBodyPolicy(), PipelinePosition.PerCall);
        if (TrafficLogPolicy.For(agent, paths, time) is { } traffic)
        {
            pipelineOptions.AddPolicy(traffic, PipelinePosition.PerTry);
        }

        if (transport is not null)
        {
            pipelineOptions.Transport = transport;
        }

        return ClientPipeline.Create(pipelineOptions, [ApiKeyAuthenticationPolicy.CreateBearerAuthorizationPolicy(new ApiKeyCredential(key))], [], []);
    }

    // Fast-model usage counts toward the chat, like other helper requests.
    private void CountUsage(JsonElement completion)
    {
        if (completion.TryGetProperty("usage", out var used))
        {
            var cached = used.TryGetProperty("prompt_tokens_details", out var details) ? Number(details, "cached_tokens") : 0;
            usage.Add(Number(used, "prompt_tokens"), Number(used, "completion_tokens"), cached);
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;
}
