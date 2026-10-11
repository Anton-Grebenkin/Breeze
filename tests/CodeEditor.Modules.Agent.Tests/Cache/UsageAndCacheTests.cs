using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Services.Anthropic;
using CodeEditor.Modules.Agent.Services.Context;
using CodeEditor.Modules.Agent.Services.Conversation;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Cache;

/// <summary>Usage accounting without double counting, and prompt cache marks for Claude.</summary>
public sealed class UsageAndCacheTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Accumulator_RepeatWithinRequest_Revises_NewInputStartsTheNext()
    {
        var usage = new UsageAccumulator();

        Assert.False(usage.Revises(Details(1_000, 2)));
        Assert.True(usage.Revises(Details(1_000, 6)));
        Assert.False(usage.Revises(Details(1_500, 3)));
    }

    // A revision replaces the request's first report at once; spend of side requests in between stays.
    [Fact]
    public void Ring_RevisionReplacesTheRequest_AndKeepsSideSpend()
    {
        var context = _fixture.Chat.Context;

        context.Add(1_000, 2, 0);
        context.AddAuxiliary(500, 10);
        context.Add(1_000, 6, 0, revisesLast: true);

        Assert.Equal((1_006L, 1_500L, 16L, 2), (context.Usage.ContextTokens, context.Usage.TotalInputTokens, context.Usage.TotalOutputTokens, context.Usage.Requests));
    }

    // Before a response the ring shows the request on its way: the last real input plus what the history gained or
    // lost since (a tool result, a compaction), so it moves at once and doesn't dip in a young chat.
    [Fact]
    public void Ring_ShowsRequestEstimate_UntilItsUsageArrives()
    {
        var context = _fixture.Chat.Context;
        _fixture.Meter.Sent(8_000);
        _fixture.Meter.Observed(10_000);
        context.Add(10_000, 500, 0);

        _fixture.Meter.Sent(9_000);
        var afterToolResult = (context.ContextTokens, context.PercentText[0]);
        _fixture.Meter.Sent(4_000);
        var afterCompaction = context.ContextTokens;
        context.Add(5_100, 200, 0);

        Assert.Equal((11_250L, '≈'), afterToolResult);
        Assert.Equal(5_000, afterCompaction);
        Assert.Equal(5_300, context.ContextTokens);
        Assert.Null(context.PendingTokens);
    }

    [Fact]
    public async Task ClaudeStyleDoubleUsage_IsNotDoubled()
    {
        _fixture.Client.ReplyWithUsages("Готово.", (6_415, 2), (6_415, 6));

        await _fixture.SendAsync("вопрос");

        Assert.Equal((6_415L, 6L, 1), (_fixture.Chat.Context.Usage.TotalInputTokens, _fixture.Chat.Context.Usage.TotalOutputTokens, _fixture.Chat.Context.Usage.Requests));
    }

    // Marks: the system prompt, the end of the previous request (the message before the model's answer) and the end.
    [Fact]
    public void CachePoints_OnSystemPreviousEndAndLastMessage()
    {
        var body = JsonNode.Parse("""
            {"model":"anthropic/claude-sonnet-5","messages":[
              {"role":"system","content":"промпт"},
              {"role":"user","content":"начало"},
              {"role":"user","content":[{"type":"text","text":"вопрос"},{"type":"text","text":"<context>"}]},
              {"role":"assistant","content":null,"tool_calls":[]},
              {"role":"tool","tool_call_id":"c1","content":"результат"}]}
            """)!;

        Assert.True(AnthropicCache.Mark(body));

        var messages = body["messages"]!.AsArray();
        Assert.Equal("ephemeral", (string?)messages[0]!["content"]![0]!["cache_control"]!["type"]);
        Assert.Equal("промпт", (string?)messages[0]!["content"]![0]!["text"]);
        Assert.Equal("начало", (string?)messages[1]!["content"]);
        Assert.Null(messages[2]!["content"]![0]!["cache_control"]);
        Assert.Equal("ephemeral", (string?)messages[2]!["content"]![1]!["cache_control"]!["type"]);
        Assert.Equal("результат", (string?)messages[4]!["content"]![0]!["text"]);
        Assert.Equal("ephemeral", (string?)messages[4]!["content"]![0]!["cache_control"]!["type"]);
        Assert.Null(messages[4]!["content"]![0]!["cache_control"]!["ttl"]);
    }

    [Fact]
    public void CachePoints_OnlyForChatRequests() =>
        Assert.False(AnthropicCache.Mark(JsonNode.Parse("""{"model":"anthropic/claude-sonnet-5","input":"x"}""")!));

    // The context ring measures against the chat limit where compaction starts, not the million-token window.
    [Fact]
    public void ContextRing_MeasuresAgainstChatLimit_AndExplainsIt()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 1_000_000 });
        var context = _fixture.Chat.Context;

        context.Add(100_000, 0, 0);

        Assert.Equal(ContextCompaction.DefaultContextLimit, context.ContextWindow);
        Assert.Equal(0.5, context.Fraction, 3);
        Assert.Contains("Предел чата", context.Hint, StringComparison.Ordinal);
        Assert.Contains("agent.contextLimit", context.Hint, StringComparison.Ordinal);
    }

    private static UsageDetails Details(long input, long output) => new() { InputTokenCount = input, OutputTokenCount = output };
}
