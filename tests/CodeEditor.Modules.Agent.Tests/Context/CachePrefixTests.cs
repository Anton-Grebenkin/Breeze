using CodeEditor.Modules.Agent.Services.Context;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Context;

/// <summary>
/// The request prefix after a compaction: later requests extend it, across approval rounds and the next message, so
/// the provider cache keeps serving the history instead of compacting it again on every run (ADR 0012).
/// </summary>
public sealed class CachePrefixTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public CachePrefixTests()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((int n, int size) => $"результат {n}\n" + new string('x', size), "read_big"));
        _fixture.Conversation.Tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((int n, int size) => $"правка {n}\n" + new string('y', size), "edit_big")));
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task AfterCompaction_ApprovalRoundsAndTheNextMessage_KeepThePrefix()
    {
        // Input budget of 17,000 tokens: old results collapse at 8,500 down to 5,100, once in this chat.
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, ContextWindow = 30_000, MaxOutputTokens = 1_000, Approvals = AgentApprovals.Full });
        for (var n = 0; n < 14; n++)
        {
            _fixture.Client.CallTool($"c{n}", n % 2 == 1 ? "edit_big" : "read_big", new Dictionary<string, object?> { ["n"] = n, ["size"] = 3_000 });
        }

        _fixture.Client.Reply("Готово.").CallTool("d0", "read_big", new Dictionary<string, object?> { ["n"] = 100, ["size"] = 3_000 }).Reply("Ещё готово.");

        await SendAsync("прочитай и поправь");
        await SendAsync("ещё");

        var requests = _fixture.Client.Requests.Select(request => request.Select(message => HistoryTranscript.Render([message])).ToList()).ToList();
        var breaks = Enumerable.Range(1, requests.Count - 1).Count(index => !requests[index].Take(requests[index - 1].Count).SequenceEqual(requests[index - 1]));
        Assert.Equal(1, breaks);
        Assert.Contains(CompactionPrompts.ToolGroupHeader, string.Join('\n', requests[^1]), StringComparison.Ordinal);
    }

    private Task SendAsync(string text)
    {
        _fixture.Chat.Input = text;
        return _fixture.Chat.SendCommand.ExecuteAsync(null);
    }
}
