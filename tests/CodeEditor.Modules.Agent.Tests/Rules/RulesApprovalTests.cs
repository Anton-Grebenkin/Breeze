using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Rules;

/// <summary>
/// agent.md in the chat: a protected path asks even with auto-accepted edits; a file rule reaches the model before the
/// edit.
/// </summary>
public sealed class RulesApprovalTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public RulesApprovalTests()
    {
        _fixture.FileSystem.AddDirectory(Path.Combine(AgentFixture.Root, ".breeze", "rules"));
        _fixture.FileSystem.AddFile(Path.Combine(AgentFixture.Root, ".breeze", "agent.md"), "---\nprotected: [\"core/**\"]\n---\nrules");
        _fixture.FileSystem.AddFile(Path.Combine(AgentFixture.Root, ".breeze", "rules", "xaml.md"), "---\napplies: \"*.xaml\"\n---\nOnly DynamicResource.");
        _fixture.Workspace.Open(AgentFixture.Root);
        var tool = new EditTool();
        _fixture.ToolProviders.Add(tool);
        _fixture.Previewers.Add(tool);
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Approvals = AgentApprovals.Auto });
    }

    private ChatViewModel Chat => _fixture.Chat;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ProtectedPath_AsksWithReason_EvenWhenEditsAreAccepted()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Args("core/A.cs")).Reply("Готово.");

        var sending = Send("поправь");
        var card = await WaitForCardAsync();
        Assert.Equal("agent.md защищает core/A.cs.", card.Reason);
        card.ApproveCommand.Execute(null);
        await sending;

        Assert.Equal(ApprovalState.Approved, card.State);
        Assert.False(card.IsAutomatic);
    }

    [Fact]
    public async Task FileRule_ComesBack_ThenEditIsApplied()
    {
        _fixture.Client.CallTool("c1", "apply_edits", Args("Main.xaml")).CallTool("c2", "apply_edits", Args("Main.xaml")).Reply("Готово.");

        await Send("поправь разметку");

        var first = Result(_fixture.Client.Requests[1]);
        Assert.Contains("Only DynamicResource.", first, StringComparison.Ordinal);
        Assert.Contains("Следуйте им и повторите изменение", first, StringComparison.Ordinal);
        Assert.Equal("ok", Result(_fixture.Client.Requests[2]));
    }

    private static string? Result(List<ChatMessage> request) =>
        request.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Last().Result?.ToString();

    private static Dictionary<string, object?> Args(string path) => new() { ["path"] = path };

    private Task Send(string text)
    {
        Chat.Input = text;
        return Chat.SendCommand.ExecuteAsync(null);
    }

    private async Task<ApprovalCardViewModel> WaitForCardAsync()
    {
        for (var i = 0; i < 200; i++)
        {
            if (Chat.Messages.LastOrDefault(message => message.Approval is { IsPending: true })?.Approval is { } card)
            {
                return card;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("No approval card.");
    }

    /// <summary>An edit tool with a preview, like apply_edits of the text editor.</summary>
    private sealed class EditTool : IAgentToolProvider, IAgentChangePreviewer
    {
        public IEnumerable<AITool> CreateTools() => [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(Edit, "apply_edits"))];

        public bool CanPreview(string toolName) => toolName == "apply_edits";

        public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FileChangePreview>>([new FileChangePreview(ProposedChangeKind.Edit, arguments["path"]!.ToString()!, "a", "b")]);

        private static string Edit(string path) => path.Length > 0 ? "ok" : "empty";
    }
}
