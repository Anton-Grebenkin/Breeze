using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Branch picker: "Create branch…" first, local branches, remote ones without a local counterpart (switching creates a
/// tracking local branch), no switch for the current branch; a new name is validated before git runs.
/// </summary>
public sealed class GitBranchPickerTests : IDisposable
{
    private const string Branches =
        "*\trefs/heads/main\t2db2835\torigin/main\tВторой\n" +
        " \trefs/heads/feature\te34d80c\t\tФункция\n" +
        " \trefs/remotes/origin/main\t2db2835\t\tВторой\n" +
        " \trefs/remotes/origin/release\t1234567\t\tВыпуск\n";

    private readonly GitPanelFixture _git = new();
    private readonly FakeQuickPick _quickPick = new();
    private readonly GitBranchPicker _picker;

    public GitBranchPickerTests() => _picker = new GitBranchPicker(_git.Repository, _git.Reader, _git.Actions, _quickPick);

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task Checkout_ListsCreateLocalAndUnpairedRemote()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner.Returns(0, Branches);

        await _picker.PickAsync();

        Assert.Equal(["+ Создать ветку…", "main", "feature", "origin/release"], _quickPick.Items.Select(item => item.Title));
        Assert.Equal("текущая • 2db2835 • Второй", _quickPick.Items[1].Detail);
        Assert.Equal("удалённая • 1234567 • Выпуск", _quickPick.Items[3].Detail);
        Assert.Equal(["for-each-ref", "--sort=-committerdate"], _git.GitArgs(0).Take(2));
    }

    [Fact]
    public async Task LocalBranch_IsSwitched_RemoteBranch_IsTracked_CurrentIsLeftAlone()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner.Returns(0, Branches);
        await _picker.PickAsync();

        await _quickPick.PickAsync("feature");
        await _quickPick.PickAsync("origin/release");
        var requests = _git.Runner.Requests.Count;
        await _quickPick.PickAsync("main");

        Assert.Contains(_git.AllGitArgs(), arguments => arguments.SequenceEqual(["switch", "feature"]));
        Assert.Contains(_git.AllGitArgs(), arguments => arguments.SequenceEqual(["switch", "--track", "origin/release"]));
        Assert.Equal(requests, _git.Runner.Requests.Count);
    }

    [Fact]
    public async Task NewBranch_OnlyValidNamesAreOffered()
    {
        await _git.OpenRepositoryAsync();

        _picker.ShowCreate();
        var invalid = _quickPick.Shown!.Filter("bad name");
        await _quickPick.PickAsync("feature/вход");

        Assert.Empty(_quickPick.Items);
        Assert.Empty(invalid);
        Assert.Equal(["switch", "-c", "feature/вход"], _git.GitArgs(0));
        Assert.Equal("Введите имя ветки: без пробелов, не с «-» или «.».", _quickPick.Shown.EmptyText);
    }

    [Fact]
    public async Task CreateItem_OpensNameInput()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner.Returns(0, Branches);
        await _picker.PickAsync();

        await _quickPick.PickAsync("Создать");

        Assert.Equal("Имя новой ветки", _quickPick.Shown!.Placeholder);
    }
}
