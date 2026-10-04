using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>A command card shows the command with Run and Allowed, not a diff with Applied.</summary>
public sealed class CommandApprovalCardTests
{
    [Fact]
    public void Command_ShownAsCommand_NotAsDiff()
    {
        var file = new FileDiffViewModel(new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, "dotnet build src/App"));

        Assert.True(file.IsCommand);
        Assert.Equal("dotnet build src/App", file.Command);
        Assert.Empty(file.Lines);
        Assert.Equal(string.Empty, file.Counts);
    }

    [Fact]
    public void CommandCard_RunAndAllowed_EditCard_ApplyAndApplied()
    {
        var command = Card(new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, "dotnet test"));
        var edit = Card(new FileChangePreview(ProposedChangeKind.Edit, "A.cs", "a", "b"));

        Assert.Equal(("Выполнить", "Применить"), (command.ApproveText, edit.ApproveText));
        command.ApproveCommand.Execute(null);
        edit.ApproveCommand.Execute(null);
        Assert.Equal(("Разрешено", "Применено"), (command.StateText, edit.StateText));
    }

    private static ApprovalCardViewModel Card(FileChangePreview change) =>
        new(new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "tool")), "Агент хочет", [new FileDiffViewModel(change)]);
}
