using CodeEditor.Modules.Agent.Services.Conversation;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>A stopped turn stays in the model history: the question, calls with results and a stop note.</summary>
public sealed class StoppedTurnHistoryTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public StoppedTurnHistoryTests() =>
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => $"содержимое {path}", "read_small"));

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task StoppedTurn_KeepsQuestionAndToolCalls_ForTheNextTurn()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client
            .CallTool("c1", "read_small", new Dictionary<string, object?> { ["path"] = "a.cs" })
            .Reply("начало", "никогда");
        _fixture.Chat.Input = "изучи a.cs";
        var sending = _fixture.Chat.SendCommand.ExecuteAsync(null);
        // The feed changes on the turn's thread (tests have no UI thread), so wait on the fake model's request count.
        await Wait.UntilAsync(() => _fixture.Client.Requests.Count == 2);

        _fixture.Chat.StopCommand.Execute(null);
        await sending;

        _fixture.Client.Gate = null;
        _fixture.Client.Reply("продолжаю");
        await _fixture.SendAsync("и что там?");

        var texts = Texts(_fixture.Client.Requests[^1]);
        Assert.Contains(texts, text => text.StartsWith("изучи a.cs", StringComparison.Ordinal));
        Assert.Contains("call:c1", texts);
        Assert.Contains("result:c1", texts);
        Assert.DoesNotContain("начало", texts);
        var note = texts.IndexOf(StoppedTurnHistory.StoppedNote);
        Assert.True(note > texts.IndexOf("result:c1"), string.Join(" | ", texts));
        Assert.Contains(texts.Skip(note + 1), text => text.StartsWith("и что там?", StringComparison.Ordinal));
    }

    // A turn cut off by a service error (402, network) stays in the history, so "continue" knows what's already done.
    [Fact]
    public async Task FailedTurn_KeepsQuestionAndToolCalls_ForTheNextTurn()
    {
        _fixture.Client
            .CallTool("c1", "read_small", new Dictionary<string, object?> { ["path"] = "a.cs" })
            .Fail(new InvalidOperationException("402 Payment Required"));
        await _fixture.SendAsync("изучи a.cs");

        _fixture.Client.Reply("продолжаю");
        await _fixture.SendAsync("продолжай");

        var texts = Texts(_fixture.Client.Requests[^1]);
        Assert.Contains(texts, text => text.StartsWith("изучи a.cs", StringComparison.Ordinal));
        Assert.Contains("result:c1", texts);
        Assert.True(texts.IndexOf(StoppedTurnHistory.FailedNote) > texts.IndexOf("result:c1"), string.Join(" | ", texts));
    }

    // Guards against re-running an approved edit after Stop: the history must hold the executed call with its result,
    // not an approved call without one.
    [Fact]
    public async Task StoppedAfterApproval_KeepsExecutedCall_AndDoesNotRunItAgain()
    {
        var edits = 0;
        _fixture.Conversation.Tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => $"изменён {path} ({++edits})", "apply_edits")));
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, AutoMemory = false, Approvals = AgentApprovals.Auto });
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client
            .CallTool("c1", "apply_edits", new Dictionary<string, object?> { ["path"] = "a.cs" })
            .Reply("начало", "никогда");
        _fixture.Chat.Input = "поправь a.cs";
        var sending = _fixture.Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => _fixture.Client.Requests.Count == 2);

        _fixture.Chat.StopCommand.Execute(null);
        await sending;

        _fixture.Client.Gate = null;
        _fixture.Client.Reply("продолжаю");
        await _fixture.SendAsync("продолжай");

        Assert.Equal(1, edits);
        var request = _fixture.Client.Requests[^1];
        Assert.DoesNotContain(request.SelectMany(message => message.Contents), content => content is ToolApprovalRequestContent or ToolApprovalResponseContent);
        var texts = Texts(request);
        Assert.Contains("call:c1", texts);
        Assert.True(texts.IndexOf(StoppedTurnHistory.StoppedNote) > texts.IndexOf("result:c1"), string.Join(" | ", texts));
    }

    [Fact]
    public async Task StoppedDuringAnswer_KeepsTheQuestionOnce()
    {
        _fixture.Client.Gate = new TaskCompletionSource();
        _fixture.Client.Reply("начало", "никогда");
        _fixture.Chat.Input = "первый";
        var sending = _fixture.Chat.SendCommand.ExecuteAsync(null);
        await Wait.UntilAsync(() => _fixture.Client.Requests.Count == 1);
        _fixture.Chat.StopCommand.Execute(null);
        await sending;

        _fixture.Client.Gate = null;
        _fixture.Client.Reply("ответ");
        await _fixture.SendAsync("второй");

        var texts = _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<TextContent>().Select(text => text.Text).ToList();
        Assert.Equal(1, texts.Count(text => text.StartsWith("первый", StringComparison.Ordinal)));
        Assert.Equal(1, texts.Count(text => text == StoppedTurnHistory.StoppedNote));
    }

    // Texts of a request, with calls and results reduced to their ids.
    private static List<string> Texts(IEnumerable<ChatMessage> request) =>
    [
        .. request.SelectMany(message => message.Contents).Select(content => content switch
        {
            TextContent text => text.Text,
            FunctionCallContent call => "call:" + call.CallId,
            FunctionResultContent result => "result:" + result.CallId,
            _ => content.GetType().Name,
        }),
    ];
}
