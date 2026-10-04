using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Panel change list: non-empty groups in order, files in natural order, a new snapshot updates rows in place (keeping
/// the selection), context keys of the selected row, the selection as the command target while the list is in use.
/// </summary>
public sealed class GitChangesViewModelTests : IDisposable
{
    private const string Conflict =
        "u UU N... 100644 100644 100644 100644 83db48f84ec878fbfb30b46d16630e944e34f205 83db48f84ec878fbfb30b46d16630e944e34f205 83db48f84ec878fbfb30b46d16630e944e34f205 c.cs";

    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task Groups_NonEmptyInOrder_FilesInNaturalOrder()
    {
        await _git.OpenRepositoryAsync(
            GitPanelFixture.Modified("file10.cs"), GitPanelFixture.Modified("b.cs", "M."), Conflict, GitPanelFixture.Untracked("file2.cs"));

        Assert.Equal([GitChangeGroup.Merge, GitChangeGroup.Staged, GitChangeGroup.Changes], _git.Changes.Groups.Select(group => group.Group));
        Assert.Equal(["file2.cs", "file10.cs"], _git.Changes.Group(GitChangeGroup.Changes).Items.Select(item => item.Path));
        Assert.Equal(["Конфликты слияния", "Изменения в индексе", "Изменения"], _git.Changes.Groups.Select(group => group.Title));
        Assert.Equal(["!", "M", "U"], new[] { _git.Item(GitChangeGroup.Merge, "c.cs"), _git.Item(GitChangeGroup.Staged, "b.cs"), _git.Item(GitChangeGroup.Changes, "file2.cs") }.Select(item => item.Letter));
        Assert.Equal("2", _git.Changes.Group(GitChangeGroup.Changes).CountText);
    }

    [Fact]
    public async Task NewStatus_UpdatesRowsInPlace_AndKeepsSelection()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs"), GitPanelFixture.Modified("b.cs"));
        var a = _git.Item(GitChangeGroup.Changes, "a.cs");
        a.IsSelected = true;
        _git.Runner.Returns(0, GitPanelFixture.Status(GitPanelFixture.Modified("a.cs", ".D"), GitPanelFixture.Untracked("c.cs")));

        await _git.Repository.RefreshAsync();

        Assert.Equal(["a.cs", "c.cs"], _git.Changes.Group(GitChangeGroup.Changes).Items.Select(item => item.Path));
        Assert.Same(a, _git.Item(GitChangeGroup.Changes, "a.cs"));
        Assert.Equal(("D", true), (a.Letter, a.IsDeleted));
        Assert.Same(a, _git.Changes.Selected);
    }

    [Fact]
    public async Task EmptyStatus_HidesGroups_AndClearsSelection()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs", "M."));
        _git.Item(GitChangeGroup.Staged, "a.cs").IsSelected = true;
        _git.Runner.Returns(0, GitPanelFixture.Status());

        await _git.Repository.RefreshAsync();

        Assert.Empty(_git.Changes.Groups);
        Assert.False(_git.Changes.HasChanges);
        Assert.Null(_git.Changes.Selected);
        Assert.Null(_git.Context.GetValue(GitContextKeys.ResourceGroup));
    }

    [Fact]
    public async Task Selection_SetsContextKeys_ForContextMenus()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs", "M."), GitPanelFixture.Modified("gone.cs", ".D"));

        _git.Item(GitChangeGroup.Staged, "a.cs").IsSelected = true;
        Assert.Equal("staged", _git.Context.GetValue(GitContextKeys.ResourceGroup));
        Assert.True(_git.Context.GetValue(GitContextKeys.ResourceIsGroup) is false);
        Assert.True(_git.Context.GetValue(GitContextKeys.ResourceDeleted) is false);

        _git.Item(GitChangeGroup.Changes, "gone.cs").IsSelected = true;
        Assert.Equal("changes", _git.Context.GetValue(GitContextKeys.ResourceGroup));
        Assert.True(_git.Context.GetValue(GitContextKeys.ResourceDeleted) is true);

        _git.Changes.Group(GitChangeGroup.Changes).IsSelected = true;
        Assert.Equal("changes", _git.Context.GetValue(GitContextKeys.ResourceGroup));
        Assert.True(_git.Context.GetValue(GitContextKeys.ResourceIsGroup) is true);
    }

    [Fact]
    public void Selection_IsTheTarget_UntilUserReturnsToText()
    {
        _git.Changes.SetFocused(true);
        var inList = _git.Changes.IsSelectionContext;

        _git.Changes.SetFocused(false);
        var paletteOpened = _git.Changes.IsSelectionContext;

        _git.Context.Set(EditorContextKeys.TextFocus, true);

        Assert.True(inList);
        Assert.True(paletteOpened);
        Assert.False(_git.Changes.IsSelectionContext);
        Assert.True(_git.Context.GetValue(GitContextKeys.ChangesFocus) is false);
    }

    [Fact]
    public async Task LargeGroup_ShowsLimitedRows_ButKeepsAllChanges()
    {
        var records = Enumerable.Range(0, GitChangeGroupViewModel.MaxItems + 10).Select(index => GitPanelFixture.Untracked($"f{index}.txt")).ToArray();

        await _git.OpenRepositoryAsync(records);

        var group = _git.Changes.Group(GitChangeGroup.Changes);
        Assert.Equal(GitChangeGroupViewModel.MaxItems, group.Items.Count);
        Assert.Equal(GitChangeGroupViewModel.MaxItems + 10, group.Changes.Count);
        Assert.Equal("Показаны первые 5000 из 5010 файлов", group.ToolTip);
    }
}
