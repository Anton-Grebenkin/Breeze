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
    public void Accumulator_RepeatWithinRequest_CountsOnce_NewRequest_Commits()
    {
        var usage = new UsageAccumulator();

        Assert.Null(usage.Add(Details(1_000, 2)));
        Assert.Null(usage.Add(Details(1_000, 6)));
        var first = usage.Add(Details(1_500, 3));

        Assert.Equal((1_000L, 6L), (first!.InputTokenCount, first.OutputTokenCount));
        Assert.Equal(1_500, usage.Flush()!.InputTokenCount);
        Assert.Null(usage.Flush());
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
