using System.ClientModel;
using System.Net;
using CodeEditor.Modules.Agent.Services.Anthropic;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// Claude via Anthropic Messages (ADR 0021): with Provod this keeps reasoning and intact call arguments. The real client
/// builds the request against a fake server that exposes the URI, headers and body and replies with Messages events.
/// </summary>
public sealed class AnthropicMessagesTests
{
    private const string Opus = "anthropic/claude-opus-5.5";

    [Fact]
    public void Request_SystemTurnsToolsThinking_AndCacheMarks()
    {
        var body = AnthropicRequest.Build(Opus, History(), Options());

        Assert.Equal("Системный промпт", (string?)body["system"]![0]!["text"]);
        Assert.NotNull(body["system"]![0]!["cache_control"]);
        var turns = body["messages"]!.AsArray();
        Assert.Equal(["user", "assistant", "user"], turns.Select(turn => (string?)turn!["role"]));
        var assistant = turns[1]!["content"]!.AsArray();
        Assert.Equal(["thinking", "text", "tool_use"], assistant.Select(block => (string?)block!["type"]));
        Assert.Equal("подпись", (string?)assistant[0]!["signature"]);
        Assert.Equal("src/a b.cs", (string?)assistant[2]!["input"]!["path"]);
        var last = turns[2]!["content"]!.AsArray();
        Assert.Equal(["tool_result", "text"], last.Select(block => (string?)block!["type"]));
        Assert.Equal("call_1", (string?)last[0]!["tool_use_id"]);
        Assert.NotNull(last[^1]!["cache_control"]);
        Assert.Equal(("adaptive", "high", 32_000), ((string?)body["thinking"]!["type"], (string?)body["output_config"]!["effort"], (int?)body["max_tokens"]));
        Assert.Equal("object", (string?)body["tools"]![0]!["input_schema"]!["type"]);
        Assert.Null(body["temperature"]);
    }

    // Agent Framework passes agent instructions as a chat option; without them Claude would have no system prompt.
    [Fact]
    public void Request_InstructionsOfTheChat_ComeFirstInSystem()
    {
        var options = Options();
        options.Instructions = "Инструкции агента";

        var system = AnthropicRequest.Build(Opus, History(), options)["system"]!.AsArray();

        Assert.Equal(["Инструкции агента", "Системный промпт"], system.Select(block => (string?)block!["text"]));
        Assert.Null(system[0]!["cache_control"]);
        Assert.NotNull(system[^1]!["cache_control"]);
    }

    // Anthropic rejects unsigned thinking blocks and empty text.
    [Fact]
    public void Request_SkipsUnsignedReasoning_AndEmptyText()
    {
        List<ChatMessage> history =
        [
            new(ChatRole.User, "вопрос"),
            new(ChatRole.Assistant, [new TextReasoningContent("без подписи"), new TextContent(" "), new TextContent("ответ")]),
        ];

        var assistant = AnthropicRequest.Build(Opus, history, Options())["messages"]![1]!["content"]!.AsArray();

        Assert.Equal(["text"], assistant.Select(block => (string?)block!["type"]));
    }

    [Fact]
    public async Task Stream_ThinkingTextAndCall_WithSpacesAtFragmentBoundaries()
    {
        var server = new MessagesServer(Stream(
            Event("message_start", """{"message":{"id":"msg_1","usage":{"input_tokens":10,"cache_read_input_tokens":900,"cache_creation_input_tokens":100,"output_tokens":1}}}"""),
            Event("content_block_start", """{"index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}"""),
            Event("content_block_delta", """{"index":0,"delta":{"type":"thinking_delta","thinking":"пла"}}"""),
            Event("content_block_delta", """{"index":0,"delta":{"type":"thinking_delta","thinking":"н"}}"""),
            Event("content_block_delta", """{"index":0,"delta":{"type":"signature_delta","signature":"подпись"}}"""),
            Event("content_block_stop", """{"index":0}"""),
            Event("content_block_start", """{"index":1,"content_block":{"type":"text","text":""}}"""),
            Event("content_block_delta", """{"index":1,"delta":{"type":"text_delta","text":"Смотрю."}}"""),
            Event("content_block_stop", """{"index":1}"""),
            Event("content_block_start", """{"index":2,"content_block":{"type":"tool_use","id":"call_1","name":"read_file","input":{}}}"""),
            Event("content_block_delta", """{"index":2,"delta":{"type":"input_json_delta","partial_json":"{\"path\": \"src/a"}}"""),
            Event("content_block_delta", """{"index":2,"delta":{"type":"input_json_delta","partial_json":" b.cs\"}"}}"""),
            Event("content_block_stop", """{"index":2}"""),
            Event("message_delta", """{"delta":{"stop_reason":"tool_use"},"usage":{"output_tokens":50}}"""),
            Event("message_stop", "{}"),
            "event: data\ndata: [DONE]\n\n"));

        var response = await server.Client(Opus).GetStreamingResponseAsync(History(), Options(), TestContext.Current.CancellationToken)
            .ToChatResponseAsync(TestContext.Current.CancellationToken);

        var contents = response.Messages.SelectMany(message => message.Contents).ToList();
        var reasoning = Assert.Single(contents.OfType<TextReasoningContent>());
        Assert.Equal(("план", "подпись"), (reasoning.Text, reasoning.ProtectedData));
        Assert.Equal("Смотрю.", response.Text);
        Assert.Equal("src/a b.cs", Assert.Single(contents.OfType<FunctionCallContent>()).Arguments!["path"]?.ToString());
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal((1010L, 900L, 50L), (response.Usage!.InputTokenCount, response.Usage.CachedInputTokenCount, response.Usage.OutputTokenCount));
        Assert.EndsWith("/v1/messages", server.Uri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(("Bearer test", AnthropicMessagesChatClient.ApiVersion), (server.Headers["Authorization"], server.Headers["anthropic-version"]));

        // The next request sends back the signed thinking before the call, which Anthropic needs to continue the turn.
        var next = AnthropicRequest.Build(Opus, [.. History(), .. response.Messages], Options())["messages"]!.AsArray();
        Assert.Equal(["thinking", "text", "tool_use"], next[^1]!["content"]!.AsArray().Select(block => (string?)block!["type"]));
    }

    [Fact]
    public async Task ErrorEvent_FailsTheTurn_WithServiceText()
    {
        var server = new MessagesServer(Stream(Event("error", """{"error":{"type":"api_error","message":"Request failed"}}""")));

        var error = await Assert.ThrowsAsync<ModelStreamException>(() =>
            server.Client(Opus).GetStreamingResponseAsync(History(), Options(), TestContext.Current.CancellationToken).ToChatResponseAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Request failed (api_error)", error.Message);
    }

    [Fact]
    public async Task HttpError_KeepsServiceText()
    {
        var server = new MessagesServer("""{"code":"unsupported_parameter","message":"Model does not support parameter output_config."}""", HttpStatusCode.BadRequest);
        var options = new AgentOptions { Endpoint = AgentServices.ProvodEndpoint, Model = Opus };

        var error = await Assert.ThrowsAsync<ClientResultException>(() =>
            server.Client(Opus).GetStreamingResponseAsync(History(), Options(), TestContext.Current.CancellationToken).ToChatResponseAsync(TestContext.Current.CancellationToken));

        Assert.Contains("does not support parameter output_config. (unsupported_parameter)", AgentErrors.Describe(error, options), StringComparison.Ordinal);
    }

    [Fact]
    public void Provod_Claude_GoesThroughMessages_ProxyApiThroughChatCompletions()
    {
        Assert.True(OpenAIChatClientFactory.UsesMessages(new AgentOptions { Endpoint = AgentServices.ProvodEndpoint, Model = Opus }));
        Assert.False(OpenAIChatClientFactory.UsesMessages(new AgentOptions { Endpoint = AgentServices.ProxyApiEndpoint, Model = "anthropic/claude-opus-5-5" }));
        Assert.False(OpenAIChatClientFactory.UsesMessages(new AgentOptions { Endpoint = AgentServices.ProvodEndpoint, Model = "x-ai/grok-4.7" }));
        Assert.False(OpenAIChatClientFactory.UsesMessages(new AgentOptions { Endpoint = AgentServices.ProvodEndpoint, Model = Opus, Api = AgentApi.ChatCompletions }));
    }

    private static ChatOptions Options() => new()
    {
        MaxOutputTokens = 32_000,
        Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
        Tools = [AIFunctionFactory.Create((string path) => path, "read_file", "Reads a file.")],
    };

    private static List<ChatMessage> History() =>
    [
        new(ChatRole.System, "Системный промпт"),
        new(ChatRole.User, "вопрос"),
        new(ChatRole.Assistant, [
            new TextReasoningContent("план") { ProtectedData = "подпись" },
            new TextContent("Смотрю."),
            new FunctionCallContent("call_1", "read_file", new Dictionary<string, object?> { ["path"] = "src/a b.cs" }),
        ]),
        new(ChatRole.Tool, [new FunctionResultContent("call_1", "текст файла")]),
        new(ChatRole.User, "дальше"),
    ];

    private static string Event(string type, string data) => MessagesServer.Event(type, data);

    private static string Stream(params string[] events) => string.Concat(events);
}
