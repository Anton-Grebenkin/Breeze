using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Git.Commands;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Shell.Menus;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// Panel and commands: every action is a "Git: …" command gated by repository state; panel buttons and list rows run the
/// same commands; the "Git" and "…" menus and the file and group context menus; Ctrl+Enter and Enter bindings.
/// </summary>
public sealed class GitPanelViewModelTests : IDisposable
{
    private readonly GitPanelFixture _git = new();
    private readonly KeybindingRegistry _keybindings = new();
    private readonly MenuRegistry _menus = new();
    private readonly GitPanelCommands _commands;
    private readonly GitMenus _gitMenus = new();
    private readonly GitPanelViewModel _panel;

    public GitPanelViewModelTests()
    {
        var picker = new GitBranchPicker(_git.Repository, _git.Reader, _git.Actions, new FakeQuickPick());
        _commands = new GitPanelCommands(_git.Repository, _git.Actions, _git.Handlers, _git.Commit, picker, _git.Tabs);
        _commands.Register(_git.Commands, _keybindings);
        _gitMenus.Register(_menus);
        var builder = new MenuBuilder(_menus, _git.Commands, _keybindings, _git.CommandService, _git.Context);
        _panel = new GitPanelViewModel(_git.Repository, _git.Changes, _git.Commit, _git.Handlers, _git.CommandService, new MenuViewModelFactory(builder, _menus, _git.Commands, _keybindings));
    }

    public void Dispose()
    {
        _panel.Dispose();
        _gitMenus.Dispose();
        _commands.Dispose();
        _git.Dispose();
    }

    // Showing the panel anywhere (or as an editor tab) reads state at once, not on the timer.
    [Fact]
    public async Task Shown_ReadsStateAtOnce()
    {
        await _git.OpenRepositoryAsync();
        _git.Runner.Returns(0, GitPanelFixture.Location).Returns(0, GitPanelFixture.Status());

        _panel.OnShown();
        await _git.Repository.Refreshing;

        Assert.NotEmpty(_git.Runner.Requests);
    }

    [Fact]
    public void AllActions_AreGitCommands()
    {
        string[] ids =
        [
            "git.commit", "git.stage", "git.unstage", "git.stageAll", "git.unstageAll", "git.discard", "git.refresh", "git.checkout",
            "git.createBranch", "git.pull", "git.push", "git.fetch", "git.showHistory", "git.openChanges", "git.openFile", "git.init",
        ];

        Assert.All(ids, id => Assert.True(_git.Commands.TryGet(id, out var command) && command.Category == "Git", id));
        Assert.Equal("Git: История", _git.Commands.Commands.Single(command => command.Id == GitCommandIds.ShowHistory).DisplayTitle);
        Assert.Equal("Ctrl+Enter", _keybindings.FindForCommand(GitCommandIds.Commit)?.Sequence.ToString());
        Assert.Equal("Enter", _keybindings.FindForCommand(GitCommandIds.OpenChanges)?.Sequence.ToString());
    }

    [Fact]
    public async Task Commands_DependOnRepositoryState()
    {
        var before = (_git.CommandService.CanExecute(GitCommandIds.Pull), _git.CommandService.CanExecute(GitCommandIds.Init));
        _git.Runner.Returns(128, "fatal: not a git repository (or any of the parent directories): .git");
        await _git.Repository.RefreshAsync();
        var outside = (_git.CommandService.CanExecute(GitCommandIds.Pull), _git.CommandService.CanExecute(GitCommandIds.Init));
        await _git.OpenRepositoryAsync();
        var inside = (_git.CommandService.CanExecute(GitCommandIds.Pull), _git.CommandService.CanExecute(GitCommandIds.Init));

        Assert.Equal((false, false), before);
        Assert.Equal((false, true), outside);
        Assert.Equal((true, false), inside);
        Assert.True(_panel.IsReady);
        Assert.Equal("main", _panel.BranchText);
    }

    [Fact]
    public async Task Buttons_RunTheSameCommands()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs"));

        await _panel.StageCommand.ExecuteAsync(_git.Item(GitChangeGroup.Changes, "src/A.cs"));
        await _panel.RunCommand.ExecuteAsync(GitCommandIds.Fetch);

        Assert.Equal(["add", "-A", "--", "src/A.cs"], _git.GitArgs(0));
        Assert.Equal(["fetch"], _git.GitArgs(2));
    }

    [Fact]
    public async Task InitButton_CreatesRepositoryInWorkspace()
    {
        _git.Runner.Returns(128, "fatal: not a git repository (or any of the parent directories): .git");
        await _git.Repository.RefreshAsync();
        _git.Runner.Requests.Clear();

        await _panel.RunCommand.ExecuteAsync(GitCommandIds.Init);

        Assert.Equal(["--no-pager", "-c", "core.quotepath=false", "-c", "color.ui=false", "init"], _git.Runner.Requests[0].Arguments);
        Assert.True(_panel.IsReady || _panel.IsNotRepository);
    }

    [Fact]
    public async Task ContextMenu_FollowsSelectedRow()
    {
        await _git.OpenRepositoryAsync(GitPanelFixture.Modified("src/A.cs", "MM"));

        _git.Item(GitChangeGroup.Staged, "src/A.cs").IsSelected = true;
        _panel.ContextMenu.Refresh();
        var stagedFile = VisibleCommands(_panel.ContextMenu);
        _git.Item(GitChangeGroup.Changes, "src/A.cs").IsSelected = true;
        _panel.ContextMenu.Refresh();
        var changedFile = VisibleCommands(_panel.ContextMenu);
        _git.Changes.Group(GitChangeGroup.Staged).IsSelected = true;
        _panel.ContextMenu.Refresh();
        var stagedGroup = VisibleCommands(_panel.ContextMenu);

        Assert.Equal(["Menu.git.openChanges", "Menu.git.openFile", "Menu.git.unstage"], stagedFile);
        Assert.Equal(["Menu.git.openChanges", "Menu.git.openFile", "Menu.git.stage", "Menu.git.discard"], changedFile);
        Assert.Equal(["Menu.git.unstageAll"], stagedGroup);
    }

    [Fact]
    public void GitMenu_IsInMenuBar_SameAsMoreMenu()
    {
        var menuBar = _menus.GetItems(MenuIds.MenuBar);

        Assert.Contains(menuBar, item => item.SubmenuId == GitMenus.Main);
        Assert.Contains(_panel.MoreMenu.Items, item => item.AutomationId == "Menu.git.showHistory");
        Assert.Contains(_panel.MoreMenu.Items, item => item.AutomationId == "Menu.git.checkout");
    }

    private static List<string> VisibleCommands(MenuViewModel menu) =>
        [.. menu.Items.Where(item => item is { IsSeparator: false, IsVisible: true }).Select(item => item.AutomationId)];
}
