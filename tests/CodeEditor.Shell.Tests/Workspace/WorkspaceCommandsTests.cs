using CodeEditor.Core.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Workspace;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Workspace;

public sealed class WorkspaceCommandsTests : IDisposable
{
    private const string Repo = @"C:\repo";
    private const string Other = @"C:\other";

    private readonly ShellFixture _shell = new();
    private readonly FakeFileDialogs _picker = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly WorkspaceCommands _commands;

    public WorkspaceCommandsTests()
    {
        _shell.FileSystem.AddDirectory(Repo).AddDirectory(Other);
        _commands = new WorkspaceCommands(_shell.Workspace, _shell.RecentFolders, _picker, _statusBar);
        _commands.Register(_shell.Commands, _shell.Keybindings, _shell.Menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _shell.Dispose();
    }

    [Fact]
    public async Task OpenFolder_UsesPickerAndRemembersFolder()
    {
        _picker.Result = Repo;

        await Execute(WorkspaceCommands.OpenFolderId);

        Assert.Equal(Repo, _shell.Workspace.Root);
        Assert.Equal([Repo], _shell.RecentFolders.Items);
        Assert.Equal("Ctrl+K Ctrl+O", _shell.Keybindings.FindForCommand(WorkspaceCommands.OpenFolderId)?.Sequence.ToString());
    }

    [Fact]
    public async Task OpenFolder_Cancelled_DoesNothing()
    {
        await Execute(WorkspaceCommands.OpenFolderId);

        Assert.Null(_shell.Workspace.Root);
    }

    [Fact]
    public async Task CloseFolder_IsAvailableOnlyWithOpenFolder()
    {
        Assert.False(_shell.CommandService.CanExecute(WorkspaceCommands.CloseFolderId));

        _commands.TryOpen(Repo);
        Assert.True(_shell.CommandService.CanExecute(WorkspaceCommands.CloseFolderId));

        await Execute(WorkspaceCommands.CloseFolderId);
        Assert.Null(_shell.Workspace.Root);
    }

    [Fact]
    public void RecentMenu_ListsFoldersNewestFirstWithClearItem()
    {
        _commands.TryOpen(Repo);
        _commands.TryOpen(Other);

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
        Assert.Contains("не найдена", _statusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentFolders_PersistAcrossInstances()
    {
        _commands.TryOpen(Repo);

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
        using var titleBar = new TitleBarViewModel(_shell.CommandService, _shell.Keybindings, _shell.Workspace, _shell.MenuFactory);

        _commands.TryOpen(Repo);

        Assert.Equal("repo", titleBar.SearchText);
        Assert.Equal("repo — Breeze", titleBar.WindowTitle);
    }

    [Fact]
    public void Welcome_ListsRecentFolders()
    {
        using var welcome = new WelcomeViewModel(_shell.Commands, _shell.Keybindings, _shell.CommandService, _shell.RecentFolders);

        _commands.TryOpen(Repo);

        var item = Assert.Single(welcome.RecentFolders);
        Assert.Equal(new RecentFolderItem("repo", Repo), item);
        Assert.True(welcome.HasRecentFolders);
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
