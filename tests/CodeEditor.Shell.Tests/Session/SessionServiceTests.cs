using CodeEditor.Core.Storage;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Session;
using CodeEditor.Shell.Tests.Editors;
using CodeEditor.Shell.Workspace;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Session;

public sealed class SessionServiceTests : IDisposable
{
    private readonly EditorAreaFixture _fixture = new();
    private readonly MemoryStore _store = new();
    private readonly RecentCommands _recentCommands = new();
    private readonly RecentFiles _recentFiles;
    private readonly UserDataPaths _paths = new(Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", Guid.NewGuid().ToString("N")));
    private readonly SessionService _session;

    public SessionServiceTests()
    {
        _recentFiles = new RecentFiles(_fixture.Area);
        var workspaceCommands = new WorkspaceCommands(
            _fixture.Workspace, new RecentFolders(_paths, NullLogger<RecentFolders>.Instance), new NoDialogs(), _fixture.StatusBar);
        _session = new SessionService(_store, _fixture.FileSystem, _fixture.Workspace, workspaceCommands, _fixture.Area, _recentCommands, _recentFiles);
    }

    public void Dispose()
    {
        _recentFiles.Dispose();
        _fixture.Dispose();
        if (Directory.Exists(_paths.Root))
        {
            Directory.Delete(_paths.Root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_CapturesFolderTabsAndRecents()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs", preview: true);
        _recentCommands.Add("theme.toggle");

        _session.Save();

        var state = _store.State!;
        Assert.Equal(EditorAreaFixture.Root, state.Folder);
        Assert.Equal([new SessionTab(EditorAreaFixture.PathOf("a.cs")), new SessionTab(EditorAreaFixture.PathOf("b.cs"), IsPreview: true)], state.Tabs);
        Assert.Equal(EditorAreaFixture.PathOf("b.cs"), state.ActiveTab);
        Assert.Equal(["theme.toggle"], state.RecentCommands);
        Assert.Equal([EditorAreaFixture.PathOf("b.cs"), EditorAreaFixture.PathOf("a.cs")], state.RecentFiles);
    }

    [Fact]
    public async Task Restore_WithoutArgument_OpensLastFolderAndTabs_SkippingDeletedFiles()
    {
        _fixture.Workspace.Close();
        _store.State = new SessionState
        {
            Folder = EditorAreaFixture.Root,
            Tabs = [new(EditorAreaFixture.PathOf("a.cs")), new(EditorAreaFixture.PathOf("gone.cs")), new(EditorAreaFixture.PathOf("c.cs"))],
            ActiveTab = EditorAreaFixture.PathOf("a.cs"),
            RecentCommands = ["x"],
        };

        _session.RestoreFolder(null);
        await _session.RestoreTabsAsync();

        Assert.Equal(EditorAreaFixture.Root, _fixture.Workspace.Root);
        Assert.Equal(["a.cs", "c.cs"], _fixture.TabNames);
        Assert.Equal("a.cs", _fixture.Area.Active?.Title);
        Assert.Equal(["x"], _recentCommands.Items);
    }

    // Editor groups survive a restart: each tab returns to its group (ADR 0031).
    [Fact]
    public async Task Groups_AreSavedAndRestored()
    {
        await _fixture.OpenAsync("a.cs");
        var b = await _fixture.OpenAsync("b.cs");
        _fixture.Area.MoveToGroup(b, _fixture.Area.AddGroup(_fixture.Area.ActiveGroup)!);
        _session.Save();
        Assert.Equal([0, 1], _store.State!.Tabs.Select(tab => tab.Group));

        await _fixture.Area.CloseAllAsync();
        _fixture.Workspace.Close();
        _session.RestoreFolder(null);
        await _session.RestoreTabsAsync();

        Assert.Equal(2, _fixture.Area.Groups.Count);
        Assert.Equal(["b.cs"], _fixture.Area.Groups[1].Tabs.Select(tab => tab.Title));
        Assert.Equal("b.cs", _fixture.Area.Active?.Title);
    }

    [Fact]
    public async Task Restore_WithOtherFolderArgument_OpensItWithoutOldTabs()
    {
        var other = Path.GetFullPath(@"C:\other");
        _fixture.FileSystem.AddDirectory(other);
        _store.State = new SessionState { Folder = EditorAreaFixture.Root, Tabs = [new(EditorAreaFixture.PathOf("a.cs"))] };

        _session.RestoreFolder(other);
        await _session.RestoreTabsAsync();

        Assert.Equal(other, _fixture.Workspace.Root);
        Assert.Empty(_fixture.TabNames);
    }

    [Fact]
    public async Task Restore_VanishedFolder_StartsEmptyWithoutError()
    {
        _fixture.Workspace.Close();
        _store.State = new SessionState { Folder = Path.GetFullPath(@"C:\gone"), Tabs = [new(@"C:\gone\a.cs")] };

        _session.RestoreFolder(null);
        await _session.RestoreTabsAsync();

        Assert.Null(_fixture.Workspace.Root);
        Assert.Equal(CodeEditor.Shell.ViewModels.StatusBarViewModel.ReadyMessage, _fixture.StatusBar.Message);
    }

    private sealed class MemoryStore : ISessionStore
    {
        public SessionState? State { get; set; }

        public SessionState? Load() => State;

        public void Save(SessionState state) => State = state;
    }

    private sealed class NoDialogs : IFileDialogs
    {
        public string? PickFolder(string title) => null;

        public string? PickFile(string title) => null;
    }
}
