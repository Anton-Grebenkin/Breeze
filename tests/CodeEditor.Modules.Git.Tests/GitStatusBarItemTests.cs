using System.Collections.Immutable;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.Git.Commands;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Status bar branch, as in VS Code: "main* ↑1 ↓2", a tooltip with the upstream, hidden outside a repository, a click
/// opens the branch picker.
/// </summary>
public sealed class GitStatusBarItemTests : IDisposable
{
    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public void Label_ShowsChangesAheadAndBehind()
    {
        var head = new GitHead("main", "2db283567e1b42094c8db604b34b7445f2614364") { Upstream = "origin/main", Ahead = 1, Behind = 2 };
        var status = new GitStatus(head, [new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, "a.cs")]);

        Assert.Equal("main* ↑1 ↓2", GitBranchLabel.Text(status));
        Assert.Equal("main", GitBranchLabel.Text(new GitStatus(head with { Ahead = 0, Behind = 0 }, ImmutableArray<GitFileChange>.Empty)));
        Assert.Equal("Ветка main — перейти на другую ветку" + Environment.NewLine + "origin/main: отправить 1, получить 2", GitBranchLabel.ToolTip(head));
        Assert.EndsWith("Нет вышестоящей ветки: Push опубликует ветку в origin", GitBranchLabel.ToolTip(head with { Upstream = null }), StringComparison.Ordinal);
        Assert.Equal("2db2835 (отсоединён)", GitBranchLabel.Name(head with { Branch = null }));
    }

    [Fact]
    public async Task Item_HiddenOutsideRepository_ClickPicksBranch()
    {
        string? executed = null;
        _git.Commands.Register(new CommandDefinition(GitCommandIds.Checkout, "Выбрать ветку", (_, _) =>
        {
            executed = GitCommandIds.Checkout;
            return ValueTask.CompletedTask;
        }));
        using var branch = new GitStatusBarItem(_git.Repository, _git.StatusBar, _git.CommandService);
        var hidden = !branch.Item.IsVisible;

        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs"));
        branch.Item.Command!.Execute(null);

        Assert.True(hidden);
        Assert.Contains(branch.Item, _git.StatusBar.Items);
        Assert.True(branch.Item.IsVisible);
        Assert.Equal("main*", branch.Item.Text);
        Assert.Equal(GitCommandIds.Checkout, executed);
        Assert.Equal("StatusBar.git.branch", branch.Item.AutomationId);
    }
}
