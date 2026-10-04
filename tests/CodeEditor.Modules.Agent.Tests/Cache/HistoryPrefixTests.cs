using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using static CodeEditor.Modules.Agent.Tests.Infrastructure.FakeModelServer;

namespace CodeEditor.Modules.Agent.Tests.Cache;

/// <summary>
/// Provider cache: every request starts with the previous one, within a turn, after an approved edit and on the next
/// question; chat requests carry the chat's cache key. The real OpenAI client builds requests against a fake server.
/// </summary>
public sealed class HistoryPrefixTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly FakeModelServer _server = new();

    public HistoryPrefixTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.ToolProviders.Add(new Tools());
    }

    public void Dispose()
    {
        _fixture.Dispose();
        _server.Dispose();
    }

    // Guards against GPT (Responses API) losing reasoning from the second step after an approval, which broke the cache.
    [Theory]
    [InlineData("openai/gpt-6-luna")]
    [InlineData("x-ai/grok-4.7")]
    [InlineData("deepseek/deepseek-v4-pro")]
    public async Task EveryRequest_StartsWithPreviousOne_AcrossApprovalAndNextQuestion(string model)
    {
        await EditWithApprovalAsync(model);
        await _fixture.SendAsync("что ещё?");

        Assert.True(_server.Bodies.Count >= 5);
        AssertEveryRequestStartsWithPrevious();
    }

    // Approvals, regular steps, then approvals again: guards against stale approvals leaking empty user messages.
    [Theory]
    [InlineData("openai/gpt-6-luna")]
    [InlineData("x-ai/grok-4.7")]
    public async Task EveryRequest_StartsWithPreviousOne_AcrossApprovalSeries(string model)
    {
        Use(model);
        _server.Enqueue(Reasoning("rs_1", "Читаю."), Call("call_1", "read_file", """{"path":"a.cs"}"""));
        for (var step = 2; step <= 5; step++)
        {
            _server.Enqueue(Reasoning("rs_" + step, "Правлю."), Message("msg_" + step, "Правка " + step), Edit(model, "call_" + step));
        }

        _server.Enqueue(Reasoning("rs_6", "Читаю."), Call("call_6", "read_file", """{"path":"b.cs"}"""));
        _server.Enqueue(Reasoning("rs_7", "Читаю."), Call("call_7", "read_file", """{"path":"c.cs"}"""));
        _server.Enqueue(Reasoning("rs_8", "Правлю."), Edit(model, "call_8"));
        _server.Enqueue(Reasoning("rs_9", "Правлю."), Edit(model, "call_9"));

        await _fixture.SendAsync("переименуй A в B");
        await _fixture.SendAsync("что ещё?");

        AssertEveryRequestStartsWithPrevious();
    }

    [Fact]
    public async Task Gpt_KeepsReasoningOfEveryStep_AfterApproval()
    {
        await EditWithApprovalAsync("openai/gpt-6-luna");

        var afterApproval = _server.History(3);
        Assert.Equal(["rs_1", "rs_2", "rs_3"], afterApproval.Where(item => (string?)item!["type"] == "reasoning").Select(item => (string?)item!["id"]));
    }

    [Fact]
    public async Task Gpt_SendsChatCacheKey_NewChatGetsAnother()
    {
        Use("openai/gpt-6-luna");

        await _fixture.SendAsync("вопрос");
        await _fixture.SendAsync("ещё вопрос");
        _fixture.Chat.NewChatCommand.Execute(null);
        await _fixture.SendAsync("новый чат");

        var keys = _server.Bodies.Select(body => (string?)JsonNode.Parse(body)!["prompt_cache_key"]).ToList();
        Assert.StartsWith("codeeditor-", keys[0], StringComparison.Ordinal);
        Assert.Equal(keys[0], keys[1]);
        Assert.NotEqual(keys[1], keys[2]);
    }

    [Fact]
    public async Task Grok_SendsChatKeyInHeaderAndSessionId()
    {
        Use("x-ai/grok-4.7");

        await _fixture.SendAsync("вопрос");

        Assert.Equal(_fixture.CacheKey.Current, _server.Headers[0][OpenAIChatClientFactory.GrokConversationHeader]);
        Assert.Equal(_fixture.CacheKey.Current, (string?)JsonNode.Parse(_server.Bodies[0])!["session_id"]);
        Assert.Null(JsonNode.Parse(_server.Bodies[0])!["prompt_cache_key"]);
    }

    // A read, two parallel reads, an edit (auto-approved in "accept immediately" mode, a new run), then the summary.
    private async Task EditWithApprovalAsync(string model)
    {
        Use(model);
        _server.Enqueue(Reasoning("rs_1", "Читаю файл."), Call("call_1", "read_file", """{"path":"a.cs"}"""));
        _server.Enqueue(Reasoning("rs_2", "Читаю ещё два."), Call("call_2", "read_file", """{"path":"b.cs"}"""), Call("call_3", "read_file", """{"path":"c.cs"}"""));
        _server.Enqueue(Reasoning("rs_3", "Правлю."), Message("msg_1", "Правлю a.cs."), Edit(model, "call_4"));
        _server.Enqueue(Reasoning("rs_4", "Готово."), Message("msg_2", "Готово."));

        await _fixture.SendAsync("переименуй A в B");
    }

    private void Use(string model) =>
        _server.Connect(_fixture, new AgentOptions { Endpoint = AgentServices.ProxyApiEndpoint, Model = model, AutoMemory = false, Approvals = AgentApprovals.Auto, ReasoningEffort = AgentReasoningEffort.High });

    // Edits use the model family's tool: a patch for GPT, replacement for the rest.
    private static JsonObject Edit(string model, string callId) => ModelProfiles.For(model).EditFormat == EditFormat.Patch
        ? Call(callId, "apply_patch", """{"patch":"*** Begin Patch"}""")
        : Call(callId, "apply_edits", """{"path":"a.cs"}""");

    private void AssertEveryRequestStartsWithPrevious()
    {
        for (var request = 1; request < _server.Bodies.Count; request++)
        {
            Assert.True(StartsWith(_server.History(request), _server.History(request - 1)), $"Request {request + 1} does not start with request {request}.");
        }
    }

    private static bool StartsWith(JsonArray history, JsonArray prefix) =>
        prefix.Count <= history.Count && prefix.Select((item, index) => JsonNode.DeepEquals(item, history[index])).All(same => same);

    private sealed class Tools : IAgentToolProvider
    {
        public IEnumerable<AITool> CreateTools() =>
        [
            new ReadOnlyAIFunction(AIFunctionFactory.Create((string path) => $"class A {{ }} // {path}", "read_file")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string patch) => "Done.", "apply_patch")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => "Done.", "apply_edits")),
        ];
    }
}
