using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Services.Anthropic;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Cache;
using CodeEditor.Modules.Agent.Services.Conversation;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Cache;

/// <summary>
/// Claude cache marks in a Messages request (ADR 0022): the system prompt and the last three turns for 5 minutes. A
/// warmup request has the same history and options, one-hour marks except on the placeholder, and a one-token answer.
/// </summary>
public sealed partial class AnthropicCacheTests
{
    private const string Sonnet = "anthropic/claude-sonnet-5.5";

    [Fact]
    public void Conversation_MarksSystemAndLastThreeTurns_ForFiveMinutes()
    {
        var body = AnthropicRequest.Build(Sonnet, History(), Options());

        Assert.Equal("ephemeral", (string?)body["system"]!.AsArray()[^1]!["cache_control"]!["type"]);
        var marks = body["messages"]!.AsArray().Select(turn => turn!["content"]!.AsArray()[^1]!["cache_control"]).ToList();
        Assert.Equal([false, false, true, true, true], marks.Select(mark => mark is not null));
        Assert.All(marks.OfType<JsonNode>(), mark => Assert.Null(mark["ttl"]));
    }

    // A thinking block can't be marked, so a turn ending with one stays unmarked.
    [Fact]
    public void ThinkingBlock_IsNotMarked()
    {
        var blocks = new JsonArray(new JsonObject { ["type"] = "thinking", ["thinking"] = "план", ["signature"] = "подпись" });

        AnthropicCache.MarkLast(blocks);

        Assert.Null(blocks[0]!["cache_control"]);
    }

    [Fact]
    public void WarmUp_MarksForAnHour_ExceptPlaceholder_AndAnswersOneToken()
    {
        List<ChatMessage> messages = [.. History(), Answer(), Placeholder()];

        var body = AnthropicRequest.BuildWarmUp(Sonnet, messages, Options());

        Assert.Equal("1h", (string?)body["system"]!.AsArray()[^1]!["cache_control"]!["ttl"]);
        var ttls = body["messages"]!.AsArray().Select(turn => (string?)turn!["content"]!.AsArray()[^1]!["cache_control"]?["ttl"]);
        Assert.Equal([null, null, null, null, "1h", "1h", null], ttls);
        Assert.Equal(1, (int?)body["max_tokens"]);
        var conversation = AnthropicRequest.Build(Sonnet, messages, Options());
        Assert.Equal(conversation["thinking"]!.ToJsonString(), body["thinking"]!.ToJsonString());
        Assert.Equal(conversation["output_config"]!.ToJsonString(), body["output_config"]!.ToJsonString());
    }

    // The next request repeats the warmup up to the model's answer, or the one-hour entry wouldn't be found.
    [Fact]
    public void WarmUp_IsThePrefixOfTheNextRequest()
    {
        var warmUp = AnthropicRequest.BuildWarmUp(Sonnet, [.. History(), Answer(), Placeholder()], Options());
        var next = AnthropicRequest.Build(Sonnet, [.. History(), Answer(), new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c3", "текст")])], Options());

        Assert.Equal(Unmarked(next["system"]), Unmarked(warmUp["system"]));
        Assert.Equal(Unmarked(next["tools"]), Unmarked(warmUp["tools"]));
        var turns = next["messages"]!.AsArray();
        var warmUpTurns = warmUp["messages"]!.AsArray();
        Assert.Equal(turns.Count, warmUpTurns.Count);
        Assert.Equal(turns.SkipLast(1).Select(Unmarked), warmUpTurns.SkipLast(1).Select(Unmarked));
    }

    [Fact]
    public async Task WarmUp_ThroughTheMessagesClient_OneTokenAndUsage()
    {
        var server = new MessagesServer(string.Concat(
            MessagesServer.Event("message_start", """{"message":{"id":"msg_1","usage":{"input_tokens":10,"cache_read_input_tokens":9500,"cache_creation_input_tokens":100,"output_tokens":1}}}"""),
            MessagesServer.Event("message_delta", """{"delta":{"stop_reason":"max_tokens"},"usage":{"output_tokens":1}}"""),
            MessagesServer.Event("message_stop", "{}")));
        var warmer = server.Client(Sonnet, AgentServices.AitunnelEndpoint).GetService<IPromptCacheWarmer>();

        var usage = await warmer!.WarmUpAsync([.. History(), Answer(), Placeholder()], Options(), TestContext.Current.CancellationToken);

        var body = Assert.Single(server.Bodies);
        Assert.Equal((1, "claude-sonnet-5.5"), ((int?)body["max_tokens"], (string?)body["model"]));
        Assert.EndsWith("/v1/messages", server.Uri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal((9_610L, 9_500L), (usage!.InputTokenCount, usage.CachedInputTokenCount));
    }

    [Fact]
    public void Wrap_OnlyClaudeThroughMessages_UnlessTurnedOff()
    {
        var warmups = new CacheWarmup(new HelperUsage(), new ManualTimeProvider(), NullLogger<CacheWarmup>.Instance);
        var server = new MessagesServer(string.Empty);

        Assert.NotNull(warmups.Wrap(server.Client(Sonnet, AgentServices.AitunnelEndpoint), new AgentOptions { Model = Sonnet }));
        Assert.Null(warmups.Wrap(server.Client(Sonnet, AgentServices.AitunnelEndpoint), new AgentOptions { Model = Sonnet, CacheWarmup = false }));
        Assert.Null(warmups.Wrap(server.Client("openai/gpt-6-luna", AgentServices.AitunnelEndpoint), new AgentOptions { Model = "openai/gpt-6-luna" }));
        Assert.Null(warmups.Wrap(server.Client("anthropic/claude-sonnet-5-5", AgentServices.ProxyApiEndpoint), new AgentOptions { Model = "anthropic/claude-sonnet-5-5" }));
    }

    private static ChatOptions Options() => new()
    {
        MaxOutputTokens = 32_000,
        Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
        Tools = [AIFunctionFactory.Create((string path) => path, "read_file", "Reads a file.")],
    };

    // Five turns: the task, two steps with calls and their results.
    private static List<ChatMessage> History() =>
    [
        new(ChatRole.System, "Системный промпт"),
        new(ChatRole.User, "задача"),
        Call("c1", "a.cs"),
        new(ChatRole.Tool, [new FunctionResultContent("c1", "текст a")]),
        Call("c2", "b.cs"),
        new(ChatRole.Tool, [new FunctionResultContent("c2", "текст b")]),
    ];

    private static ChatMessage Call(string id, string path) => new(ChatRole.Assistant,
    [
        new TextReasoningContent("план") { ProtectedData = "подпись" },
        new TextContent("Читаю."),
        new FunctionCallContent(id, "read_file", new Dictionary<string, object?> { ["path"] = path }),
    ]);

    private static ChatMessage Answer() => Call("c3", "c.cs");

    private static ChatMessage Placeholder() => new(ChatRole.User, [new FunctionResultContent("c3", "pending"), new TextContent("…")]);

    private static string Unmarked(JsonNode? node) => CacheControl().Replace(node!.ToJsonString(), string.Empty);

    [GeneratedRegex(@",""cache_control"":\{[^}]*\}")]
    private static partial Regex CacheControl();
}
