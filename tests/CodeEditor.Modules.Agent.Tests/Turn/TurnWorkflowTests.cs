using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.Workflow;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>Turn graph (ADR 0013): nodes and edges; turn scenarios through the graph live in the other tests.</summary>
public sealed class TurnWorkflowTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Graph_HasModelApprovalsChecks_AndUserPort()
    {
        var approvals = new ApprovalExecutor(new ApprovalCards([], []), _fixture.Deep, new AgentActivityLog(_fixture.ActivityLog), new HashSet<string>(StringComparer.Ordinal), acceptEdits: false, AgentMode.Agent, CancellationToken.None);
        var workflow = AgentTurnWorkflow.Build(
            new ModelExecutor(_fixture.Conversation, CancellationToken.None),
            approvals,
            new ChecksExecutor(_fixture.Checks, _fixture.Budget, _fixture.Deep, _fixture.Queue, _fixture.Images, CancellationToken.None));

        var mermaid = AgentTurnWorkflow.Mermaid(workflow);

        Assert.Equal(ModelExecutor.NodeId, workflow.StartExecutorId);
        Assert.Contains("model -. conditional .-> approvals", mermaid, StringComparison.Ordinal);
        Assert.Contains("model -. conditional .-> checks", mermaid, StringComparison.Ordinal);
        Assert.Contains("approvals --> approve", mermaid, StringComparison.Ordinal);
        Assert.Contains("approve --> approvals", mermaid, StringComparison.Ordinal);
        Assert.Contains("approvals --> model", mermaid, StringComparison.Ordinal);
        Assert.Contains("checks --> model", mermaid, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelOutcome_NeedsApproval_OnlyWithRequests()
    {
        Assert.False(new ModelOutcome([], ChatFinishReason.Stop, "ответ").NeedsApproval);
        Assert.True(new ModelOutcome([new ToolApprovalRequestContent("c1", new FunctionCallContent("c1", "apply_edits"))], null, string.Empty).NeedsApproval);
    }

    // A turn through the graph produces the same feed as before.
    [Fact]
    public async Task Turn_RunsThroughTheGraph_FeedUnchanged()
    {
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => "ok " + path, "probe"));
        _fixture.Client
            .SayAndCallTool("Смотрю.", "c1", "probe", new Dictionary<string, object?> { ["path"] = "a" })
            .Reply("Готово.");

        await _fixture.SendAsync("проверь");

        Assert.Equal(
            [(ChatMessageKind.User, "проверь"), (ChatMessageKind.Progress, "Смотрю."), (ChatMessageKind.Tool, "probe(path: a)"), (ChatMessageKind.Assistant, "Готово.")],
            _fixture.Chat.Messages.Select(message => (message.Kind, message.Text)));
        Assert.Equal(2, _fixture.Client.Requests.Count);
    }
}
