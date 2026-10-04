using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels.Tabs;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// File commands: the target is the argument, the selected row (while the list is in use) or the active editor's file;
/// discard asks first and sends new files to the recycle bin; "stage all" without conflicts is one command; changes open
/// in a tab by id, with a separate tab for the index.
/// </summary>
public sealed class GitChangeHandlersTests : IDisposable
{
    private static readonly GitFileChange WorkTreeChange = new(GitChangeGroup.Changes, GitFileStatus.Modified, "src/A.cs");

    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task Stage_SelectedRow_WhileListHasFocus()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"), GitPanelFixture.Untracked("new.txt"));
        _git.Item(GitChangeGroup.Changes, "new.txt").IsSelected = true;
        _git.Changes.SetFocused(true);

        await _git.Handlers.StageAsync(null);

        Assert.Equal(["add", "-A", "--", "new.txt"], _git.GitArgs(0));
    }

    [Fact]
    public async Task Stage_AfterReturningToText_UsesActiveEditorFile()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"), GitPanelFixture.Untracked("new.txt"));
        _git.Item(GitChangeGroup.Changes, "new.txt").IsSelected = true;
        _git.Changes.SetFocused(true);
        await _git.Editors.OpenTextAsync(new OpenFileRequest(Path.Combine(GitPanelFixture.Root, "src", "A.cs")));
        _git.Context.Set(EditorContextKeys.TextFocus, true);

        await _git.Handlers.StageAsync(null);

        Assert.Equal(["add", "-A", "--", "src/A.cs"], _git.GitArgs(0));
    }

    [Fact]
    public async Task NothingToStage_IsReported()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs", "M."));

        await _git.Handlers.StageAsync(new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Modified, "src/A.cs"));

        Assert.Empty(_git.Runner.Requests);
        Assert.Equal("Нет изменений для добавления в индекс.", _git.StatusBar.Message);
    }

    [Fact]
    public async Task Unstage_BeforeFirstCommit_RemovesFromIndexOnly()
    {
        _git.Runner.Returns(0, GitPanelFixture.Location).Returns(0, "# branch.oid (initial)\0# branch.head main\0" + GitPanelFixture.Modified("a.cs", "A.") + "\0");
        await _git.Repository.RefreshAsync();
        _git.Runner.Requests.Clear();

        await _git.Handlers.UnstageAsync(new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Added, "a.cs"));

        Assert.Equal(["rm", "--cached", "-r", "-q", "--", "a.cs"], _git.GitArgs(0));
    }

    [Fact]
    public async Task DiscardGroup_AsksFirst_RestoresTrackedAndRecyclesNewFiles()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"), GitPanelFixture.Untracked("new.txt"), GitPanelFixture.Modified("b.cs", "M."));

        await _git.Handlers.DiscardAsync(_git.Changes.Group(GitChangeGroup.Changes));

        Assert.Equal(["Отменить изменения в 2 файлах?"], _git.Dialogs.Confirmations);
        Assert.Equal(["restore", "--worktree", "--", "src/A.cs"], _git.GitArgs(0));
        Assert.Equal([Path.Combine(GitPanelFixture.Root, "new.txt")], _git.FileSystem.RecycledPaths);
    }

    [Fact]
    public async Task Discard_Declined_ChangesNothing()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));
        _git.Dialogs.ConfirmAnswer = false;

        await _git.Handlers.DiscardAsync(WorkTreeChange);

        Assert.Equal(["Отменить изменения в «src/A.cs»?"], _git.Dialogs.Confirmations);
        Assert.Empty(_git.Runner.Requests);
    }

    [Fact]
    public async Task DiscardNewFile_AsksToDelete()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Untracked("new.txt"));

        await _git.Handlers.DiscardAsync(new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Untracked, "new.txt"));

        Assert.Equal(["Удалить новый файл «new.txt»?"], _git.Dialogs.Confirmations);
        Assert.DoesNotContain(_git.Runner.Requests, request => request.Arguments.Contains("restore"));
        Assert.Single(_git.FileSystem.RecycledPaths);
    }

    [Fact]
    public async Task StageAll_WithoutConflicts_IsOneCommand_WithConflictsOnlyGroupFiles()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"), GitPanelFixture.Untracked("new.txt"));
        _git.Runner.Returns(0, string.Empty).Returns(0, GitPanelFixture.Status(GitPanelFixture.Modified("src/A.cs"), "u UU N... 1 1 1 1 h h h c.cs"));

        await _git.Handlers.StageAllAsync(null);
        await _git.Handlers.StageAllAsync(GitChangeGroup.Changes);

        Assert.Equal(["add", "-A"], _git.GitArgs(0));
        Assert.Equal(["add", "-A", "--", "src/A.cs"], _git.GitArgs(2));
    }

    [Fact]
    public async Task UnstageAll_ResetsIndex()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs", "M."));

        await _git.Handlers.UnstageAllAsync();

        Assert.Equal(["reset", "-q"], _git.GitArgs(0));
    }

    [Fact]
    public async Task OpenChanges_OpensTabById_IndexSeparately()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs", "MM"));

        await _git.Handlers.OpenChangesAsync(WorkTreeChange, preview: true);
        await ((GitDiffViewModel)_git.Editors.Tabs[0].Editor).Loaded;
        await _git.Handlers.OpenChangesAsync(new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Modified, "src/A.cs"), preview: false);
        await ((GitDiffViewModel)_git.Editors.Tabs[1].Editor).Loaded;

        Assert.Equal(["git.diff:src/A.cs", "git.diff.staged:src/A.cs"], _git.Editors.Tabs.Select(tab => tab.Key));
        Assert.Equal(["A.cs (изменения)", "A.cs (индекс)"], _git.Editors.Tabs.Select(tab => tab.Title));
        Assert.True(_git.Editors.Tabs[0].IsPreview);
        Assert.Equal(["diff", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "src/A.cs"], _git.GitArgs(0));
        Assert.Equal(["diff", "--cached", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "src/A.cs"], _git.GitArgs(1));
    }

    [Fact]
    public async Task OpenFile_OpensWorkingFile_DeletedFileIsReported()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"), GitPanelFixture.Modified("gone.cs", ".D"));

        await _git.Handlers.OpenFileAsync(WorkTreeChange);
        await _git.Handlers.OpenFileAsync(new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Deleted, "gone.cs"));

        Assert.Equal([Path.Combine(GitPanelFixture.Root, "src", "A.cs")], _git.OpenedFiles);
        Assert.Equal("Файл удалён — можно открыть только изменения.", _git.StatusBar.Message);
    }
}
