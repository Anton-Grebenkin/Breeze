using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// Request fields by service dialect (ADR 0021): ProxyAPI gets the Claude and Grok extensions and its Responses fields,
/// strict Provod only what it accepts, and a user endpoint the standard OpenAI fields.
/// </summary>
public sealed class ServiceDialectTests
{
    private const string UserEndpoint = "http://localhost:8080/v1";
    private const string ChatKey = "codeeditor-test";

    [Fact]
    public async Task Gpt_ProxyApi_StatelessWithEncryptedReasoning_CacheKeyAndPhases()
    {
        var (body, _) = await CaptureAsync(AgentServices.ProxyApiEndpoint, "openai/gpt-6-luna");

        Assert.False((bool)body["store"]!);
        Assert.Contains("reasoning.encrypted_content", body["include"]!.AsArray().Select(item => (string?)item));
        Assert.Equal(ChatKey, (string?)body["prompt_cache_key"]);
        Assert.Contains(Input(body), item => (string?)item["type"] == "reasoning");
        Assert.Contains(Input(body), item => item["phase"] is not null);
    }

    // Provod rejects store, include and prompt_cache_key (400) and can't resolve unencrypted reasoning; phases are fine.
    [Fact]
    public async Task Gpt_Provod_WithoutRejectedFields_ReasoningLeftOutOfHistory_PhasesKept()
    {
        var (body, _) = await CaptureAsync(AgentServices.ProvodEndpoint, "openai/gpt-6-luna");

        Assert.All(new[] { "store", "include", "prompt_cache_key" }, field => Assert.Null(body[field]));
        Assert.DoesNotContain(Input(body), item => (string?)item["type"] == "reasoning");
        Assert.Contains(Input(body), item => item["phase"] is not null);
        Assert.Contains(Input(body), item => (string?)item["type"] == "function_call");
    }

    [Fact]
    public async Task Gpt_UserEndpoint_GetsStandardOpenAIFields()
    {
        var (body, _) = await CaptureAsync(UserEndpoint, "openai/gpt-6-luna");

        Assert.False((bool)body["store"]!);
        Assert.Equal(ChatKey, (string?)body["prompt_cache_key"]);
    }

    // With Provod, Claude uses Messages (AnthropicMessagesTests), where thinking and cache marks are native fields.
    [Theory]
    [InlineData(AgentServices.ProxyApiEndpoint, true)]
    [InlineData(AgentServices.ProvodEndpoint, true)]
    [InlineData(AgentServices.AitunnelEndpoint, true)]
    [InlineData(UserEndpoint, false)]
    public async Task Claude_GetsCacheMarksAndThinking_OnlyWhereAccepted(string endpoint, bool extensions)
    {
        var (body, _) = await CaptureAsync(endpoint, "anthropic/claude-sonnet-5");

        Assert.Equal(extensions, body["thinking"] is not null);
        Assert.Equal(extensions, body.ToJsonString().Contains("cache_control", StringComparison.Ordinal));
    }

    // Chat Completions takes effort as reasoning_effort; Provod's Claude uses Messages, where it's output_config.
    [Theory]
    [InlineData(AgentServices.ProxyApiEndpoint, true)]
    [InlineData(UserEndpoint, true)]
    public async Task Claude_GetsReasoningEffort_ThroughChatCompletions(string endpoint, bool effort)
    {
        var (body, _) = await CaptureAsync(endpoint, "anthropic/claude-sonnet-5");

        Assert.Equal(effort, body["reasoning_effort"] is not null);
    }

    // Grok at Provod: the answer limit is max_tokens only, and reasoning_effort returns 400.
    [Theory]
    [InlineData(AgentServices.ProxyApiEndpoint, false)]
    [InlineData(AgentServices.ProvodEndpoint, true)]
    public async Task Grok_Provod_LegacyMaxTokens_NoReasoningEffort(string endpoint, bool provod)
    {
        var (body, _) = await CaptureAsync(endpoint, "x-ai/grok-4.7");

        Assert.Equal(32_000, (int?)body[provod ? "max_tokens" : "max_completion_tokens"]);
        Assert.Null(body[provod ? "max_completion_tokens" : "max_tokens"]);
        Assert.Equal(provod, body["reasoning_effort"] is null);
    }

    [Theory]
    [InlineData(AgentServices.ProxyApiEndpoint, true)]
    [InlineData(AgentServices.ProvodEndpoint, false)]
    [InlineData(UserEndpoint, false)]
    public async Task Grok_GetsSessionKey_OnlyWhereAccepted(string endpoint, bool sessionKey)
    {
        var (body, headers) = await CaptureAsync(endpoint, "x-ai/grok-4.7");

        Assert.Equal(sessionKey, body["session_id"] is not null);
        Assert.Equal(sessionKey, headers.Contains(OpenAIChatClientFactory.GrokConversationHeader));
    }

    // The LiteLLM placeholder is a ProxyAPI quirk; other services don't buffer the answer to look for it.
    [Theory]
    [InlineData(AgentServices.ProxyApiEndpoint, true)]
    [InlineData(AgentServices.ProvodEndpoint, false)]
    public void PlaceholderFilter_OnlyWherePlaceholderComes(string endpoint, bool filtered)
    {
        var options = new AgentOptions { Endpoint = endpoint, Model = "anthropic/claude-sonnet-5" };
        using var client = OpenAIChatClientFactory.Create(options, new ApiKeyCredential("test"), OpenAIChatClientFactory.ClientOptions(options, new Uri(endpoint)));

        Assert.Equal(filtered, client.GetService<ProxyPlaceholderChatClient>() is not null);
    }

    // AITUNNEL expects its catalog name; a slashed id would bypass the catalog and its prices via OpenRouter.
    [Theory]
    [InlineData("openai/gpt-6-luna", "gpt-6-luna")]
    [InlineData("x-ai/grok-4.7", "grok-4.7")]
    [InlineData("anthropic/claude-opus-5.5", "claude-opus-5.5")]
    public async Task Aitunnel_GetsShortModelId_GrokSessionKey_AndMaxTokens(string model, string onTheWire)
    {
        var (body, _) = await CaptureAsync(AgentServices.AitunnelEndpoint, model);

        Assert.Equal(onTheWire, (string?)body["model"]);
        if (model.StartsWith("x-ai/", StringComparison.Ordinal))
        {
            Assert.Equal(ChatKey, (string?)body["session_id"]);
            Assert.Equal(32_000, (int?)body["max_tokens"]);
            Assert.Null(body["max_completion_tokens"]);
        }
    }

    [Fact]
    public void UnknownAddress_HasStandardDialect() =>
        Assert.Same(ServiceDialect.Standard, AgentServices.DialectFor(UserEndpoint));

    private static IEnumerable<JsonNode> Input(JsonNode body) => body["input"]!.AsArray().OfType<JsonNode>();

    // A step with reasoning, a preamble and a tool call: everything the dialect fields affect.
    private static ChatMessage[] History() =>
    [
        new(ChatRole.User, "вопрос"),
        new(ChatRole.Assistant, [
            new TextReasoningContent("план") { ProtectedData = "encrypted" },
            new TextContent("Смотрю файл."),
            new FunctionCallContent("call_1", "read_file", new Dictionary<string, object?> { ["path"] = "a.cs" }),
        ]),
        new(ChatRole.Tool, [new FunctionResultContent("call_1", "текст файла")]),
    ];

    private static async Task<(JsonNode Body, IReadOnlySet<string> Headers)> CaptureAsync(string endpoint, string model)
    {
        var options = new AgentOptions { Endpoint = endpoint, Model = model };
        var handler = new CapturingHandler();
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, new Uri(endpoint), () => ChatKey);
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        using var client = OpenAIChatClientFactory.Create(options, new ApiKeyCredential("test"), clientOptions);

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync(History(), new ChatOptions { MaxOutputTokens = 32_000, Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High } }, TestContext.Current.CancellationToken));
        return (JsonNode.Parse(handler.Body!)!, handler.Headers);
    }

    /// <summary>Records the request body and headers and fails, since the tests don't need a model answer.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public HashSet<string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Headers.UnionWith(request.Headers.Select(header => header.Key));
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":{"message":"test"}}""") };
        }
    }
}
