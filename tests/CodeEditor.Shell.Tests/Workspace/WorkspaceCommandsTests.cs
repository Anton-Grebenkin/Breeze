using CodeEditor.Core.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Session;
using CodeEditor.Shell.Tests.Editors;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.Tests.Session;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Workspace;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Workspace;

public sealed class WorkspaceCommandsTests : IDisposable
{
    private const string Repo = @"C:\repo";
    private const string Other = @"C:\other";

    private readonly ShellFixture _shell = new();
    private readonly EditorAreaFixture _editors = new();
    private readonly FakeFileDialogs _picker = new();
    private readonly FakeAppWindows _windows = new();
    private readonly WorkspaceSwitcher _switcher;
    private readonly WorkspaceCommands _commands;

    public WorkspaceCommandsTests()
    {
        _editors.Workspace.Close();
        _editors.FileSystem.AddDirectory(Repo).AddDirectory(Other);
        _switcher = new WorkspaceSwitcher(
            _editors.Workspace,
            _editors.FileSystem,
            _shell.RecentFolders,
            _editors.StatusBar,
            _editors.Area,
            new MemorySessionStore(),
            new FolderTabs(_editors.Area, _editors.FileSystem),
            _windows);
        _commands = new WorkspaceCommands(_editors.Workspace, _switcher, _shell.RecentFolders, _picker, _windows);
        _commands.Register(_shell.Commands, _shell.Keybindings, _shell.Menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _editors.Dispose();
        _shell.Dispose();
    }

    [Fact]
    public async Task OpenFolder_UsesPickerAndRemembersFolder()
    {
        _picker.Result = Repo;

        await Execute(WorkspaceCommands.OpenFolderId);

        Assert.Equal(Repo, _editors.Workspace.Root);
        Assert.Equal([Repo], _shell.RecentFolders.Items);
        Assert.Equal("Ctrl+K Ctrl+O", _shell.Keybindings.FindForCommand(WorkspaceCommands.OpenFolderId)?.Sequence.ToString());
    }

    [Fact]
    public async Task OpenFolder_Cancelled_DoesNothing()
    {
        await Execute(WorkspaceCommands.OpenFolderId);

        Assert.Null(_editors.Workspace.Root);
    }

    [Fact]
    public async Task CloseFolder_IsAvailableOnlyWithOpenFolder()
    {
        // The workspace sets its context key in the editor fixture's context.
        var commands = new CommandService(_shell.Commands, _editors.Context, NullLogger<CommandService>.Instance);
        Assert.False(commands.CanExecute(WorkspaceCommands.CloseFolderId));

        _switcher.TryOpen(Repo);
        Assert.True(commands.CanExecute(WorkspaceCommands.CloseFolderId));

        Assert.Equal(CommandExecutionStatus.Succeeded, await commands.ExecuteAsync(WorkspaceCommands.CloseFolderId, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(_editors.Workspace.Root);
    }

    [Fact]
    public void RecentMenu_ListsFoldersNewestFirstWithClearItem()
    {
        _switcher.TryOpen(Repo);
        _switcher.TryOpen(Other);

        var items = _shell.MenuBuilder.Build(WorkspaceCommands.RecentMenuId);

        Assert.Equal([Other, Repo, "—", "_Очистить список"], items.Select(item => item.ToString()));
    }

    [Fact]
    public void RecentMenu_IsHiddenWhenEmpty()
    {
        var file = _shell.MenuBuilder.Build(MenuIds.File);

        Assert.DoesNotContain(file, item => item.AutomationId == $"Menu.{WorkspaceCommands.RecentMenuId}");
    }

    [Fact]
    public async Task OpenRecent_MissingFolder_IsRemovedAndReported()
    {
        _shell.RecentFolders.Add(@"C:\gone");

        var status = await _shell.CommandService.ExecuteAsync(
            WorkspaceCommands.OpenRecentId, @"C:\gone", TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Succeeded, status);
        Assert.Empty(_shell.RecentFolders.Items);
        Assert.Contains("не найдена", _editors.StatusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentFolders_PersistAcrossInstances()
    {
        _switcher.TryOpen(Repo);

        var reloaded = new RecentFolders(_shell.Paths, NullLogger<RecentFolders>.Instance);

        Assert.Equal([Repo], reloaded.Items);
    }

    [Fact]
    public void RecentFolders_AreCaseInsensitiveAndCapped()
    {
        for (var i = 0; i < RecentFolders.Capacity + 3; i++)
        {
            _shell.RecentFolders.Add($@"C:\p{i}");
        }

        _shell.RecentFolders.Add(@"c:\P5");

        Assert.Equal(RecentFolders.Capacity, _shell.RecentFolders.Items.Count);
        Assert.Equal(@"c:\P5", _shell.RecentFolders.Items[0]);
        Assert.Single(_shell.RecentFolders.Items, item => item.Equals(@"C:\p5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TitleBar_ShowsFolderName()
    {
        using var titleBar = new TitleBarViewModel(_shell.CommandService, _shell.Keybindings, _editors.Workspace, _shell.MenuFactory);

        _switcher.TryOpen(Repo);

        Assert.Equal("repo", titleBar.SearchText);
        Assert.Equal("repo — Breeze", titleBar.WindowTitle);
    }

    [Fact]
    public void Welcome_ListsRecentFolders()
    {
        using var welcome = new WelcomeViewModel(_shell.Commands, _shell.Keybindings, _shell.CommandService, _shell.RecentFolders);

        _switcher.TryOpen(Repo);

        var item = Assert.Single(welcome.RecentFolders);
        Assert.Equal(new RecentFolderItem("repo", Repo), item);
        Assert.True(welcome.HasRecentFolders);
    }

    [Fact]
    public async Task NewWindow_OpensEmptyWindow_CtrlShiftN()
    {
        await Execute(WorkspaceCommands.NewWindowId);

        Assert.Equal([null], _windows.Opened);
        Assert.Equal("Ctrl+Shift+N", _shell.Keybindings.FindForCommand(WorkspaceCommands.NewWindowId)?.Sequence.ToString());
    }

    [Fact]
    public async Task OpenFolderInNewWindow_OpensItThere_ThisWindowUnchanged()
    {
        _picker.Result = Repo;

        await Execute(WorkspaceCommands.OpenFolderInNewWindowId);

        Assert.Equal([Repo], _windows.Opened);
        Assert.Null(_editors.Workspace.Root);
    }

    [Fact]
    public async Task OpenFolderInNewWindow_AlreadyOpen_ActivatesThatWindow()
    {
        _picker.Result = Repo;
        _windows.OtherFolders.Add(Repo);

        await Execute(WorkspaceCommands.OpenFolderInNewWindowId);

        Assert.Equal([Repo], _windows.Activated);
        Assert.Empty(_windows.Opened);
    }

    [Fact]
    public void FileMenu_StartsWithNewWindowAndFolderCommands()
    {
        var file = _shell.MenuBuilder.Build(MenuIds.File).Select(item => item.ToString()).ToList();

        Assert.Equal(["Но_вое окно", "_Открыть папку…", "Открыть папку в новом о_кне…"], file.Take(3));
    }

    private async Task Execute(string commandId)
    {
        var status = await _shell.CommandService.ExecuteAsync(commandId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandExecutionStatus.Succeeded, status);
    }

    private sealed class FakeFileDialogs : IFileDialogs
    {
        public string? Result { get; set; }

        public string? PickFolder(string title) => Result;

        public string? PickFile(string title) => Result;
    }
}
