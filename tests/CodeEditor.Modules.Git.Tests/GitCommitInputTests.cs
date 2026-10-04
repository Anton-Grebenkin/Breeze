using CodeEditor.Modules.Git.Services;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Committing from the panel, as in VS Code: the index is committed; an empty index with changes asks and commits all;
/// no message or no changes means no commit; a message with quotes and newlines goes as one argument.
/// </summary>
public sealed class GitCommitInputTests : IDisposable
{
    private const string Message = "Исправить \"расчёт\" скидки\n\nПодробности";

    private readonly GitPanelFixture _git = new();

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task StagedChanges_AreCommitted_AndMessageCleared()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs", "M."));
        _git.Commit.Message = Message;

        var committed = await _git.Commit.CommitAsync();

        Assert.True(committed);
        Assert.Equal(["commit", "-m", Message], _git.GitArgs(0));
        Assert.Empty(_git.Dialogs.Confirmations);
        Assert.Empty(_git.Commit.Message);
        Assert.Equal("Git: Коммит — готово", _git.StatusBar.Message);
    }

    [Fact]
    public async Task EmptyIndex_AsksAndCommitsAllChanges()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs"), GitPanelFixture.Untracked("new.txt"));
        _git.Commit.Message = Message;

        await _git.Commit.CommitAsync();

        Assert.Equal(["В индексе нет изменений. Добавить в индекс все изменения и закоммитить их?"], _git.Dialogs.Confirmations);
        Assert.Equal(["add", "-A"], _git.GitArgs(0));
        Assert.Equal(["commit", "-m", Message], _git.GitArgs(1));
    }

    [Fact]
    public async Task EmptyIndex_Declined_RunsNothing_KeepsMessage()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs"));
        _git.Commit.Message = Message;
        _git.Dialogs.ConfirmAnswer = false;

        var committed = await _git.Commit.CommitAsync();

        Assert.False(committed);
        Assert.Empty(_git.Runner.Requests);
        Assert.Equal(Message, _git.Commit.Message);
    }

    [Fact]
    public async Task NoMessage_IsAnError_AndFocusesTheField()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs", "M."));
        var focused = false;
        _git.Commit.FocusRequested += (_, _) => focused = true;

        var committed = await _git.Commit.CommitAsync();

        Assert.False(committed);
        Assert.True(focused);
        Assert.Equal("Введите сообщение коммита.", _git.Repository.Error);
        Assert.Empty(_git.Runner.Requests);
    }

    [Fact]
    public async Task NoChanges_NothingToCommit()
    {
        await _git.OpenRepositoryAsync();
        _git.Commit.Message = Message;

        var committed = await _git.Commit.CommitAsync();

        Assert.False(committed);
        Assert.Equal("Нет изменений для коммита.", _git.StatusBar.Message);
        Assert.Empty(_git.Runner.Requests);
    }

    [Fact]
    public async Task FailedCommit_KeepsMessage()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("a.cs", "M."));
        _git.Commit.Message = Message;
        _git.Runner.Returns(128, "Author identity unknown");

        var committed = await _git.Commit.CommitAsync();

        Assert.False(committed);
        Assert.Equal(Message, _git.Commit.Message);
        Assert.Equal("Author identity unknown", _git.Repository.Error);
    }

    [Fact]
    public async Task Placeholder_NamesTheBranch()
    {
        await _git.OpenRepositoryAsync();

        Assert.Equal("Сообщение (Ctrl+Enter — коммит в «main»)", _git.Commit.Placeholder);
        Assert.Equal(GitRepositoryState.Ready, _git.Repository.State);
    }
}
