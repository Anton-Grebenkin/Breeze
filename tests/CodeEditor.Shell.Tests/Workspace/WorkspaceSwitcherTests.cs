using CodeEditor.Core.Storage;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Session;
using CodeEditor.Shell.Tests.Editors;
using CodeEditor.Shell.Tests.Session;
using CodeEditor.Shell.Workspace;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Workspace;

/// <summary>Switching the window's folder: tabs belong to the folder, as in VS Code.</summary>
public sealed class WorkspaceSwitcherTests : IDisposable
{
    private static readonly string Other = Path.GetFullPath(@"C:\other");

    private readonly EditorAreaFixture _fixture = new();
    private readonly MemorySessionStore _sessions = new();
    private readonly FakeAppWindows _windows = new();
    private readonly UserDataPaths _paths = new(Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", Guid.NewGuid().ToString("N")));
    private readonly RecentFolders _recent;
    private readonly WorkspaceSwitcher _switcher;

    public WorkspaceSwitcherTests()
    {
        _fixture.FileSystem.AddFile(Path.Combine(Other, "x.cs"), "class X {}");
        _recent = new RecentFolders(_paths, NullLogger<RecentFolders>.Instance);
        _switcher = new WorkspaceSwitcher(
            _fixture.Workspace, _fixture.FileSystem, _recent, _fixture.StatusBar, _fixture.Area, _sessions, new FolderTabs(_fixture.Area, _fixture.FileSystem), _windows);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        if (Directory.Exists(_paths.Root))
        {
            Directory.Delete(_paths.Root, recursive: true);
        }
    }

    [Fact]
    public async Task Open_OtherFolder_SavesOldTabsClosesThemAndRestoresItsOwn()
    {
        await _fixture.OpenAsync("a.cs");
        _sessions.SaveFolder(new FolderSession { Folder = Other, Tabs = [new(Path.Combine(Other, "x.cs"))] });

        Assert.True(await _switcher.OpenAsync(Other));

        Assert.Equal(Other, _fixture.Workspace.Root);
        Assert.Equal(["x.cs"], _fixture.TabNames);
        Assert.Equal([new SessionTab(EditorAreaFixture.PathOf("a.cs"))], _sessions.Folders[EditorAreaFixture.Root].Tabs);
    }

    [Fact]
    public async Task Open_UnsavedFile_CancelKeepsFolderAndTabs()
    {
        (await _fixture.OpenAsync("a.cs")).Document.Buffer.Replace(0, 0, "// ");
        _fixture.Dialogs.SaveAnswer = SaveChoice.Cancel;

        Assert.False(await _switcher.OpenAsync(Other));

        Assert.Equal(EditorAreaFixture.Root, _fixture.Workspace.Root);
        Assert.Equal(["a.cs"], _fixture.TabNames);
        Assert.Empty(_sessions.Folders);
    }

    [Fact]
    public async Task Open_UnsavedFile_DontSaveSwitches()
    {
        (await _fixture.OpenAsync("a.cs")).Document.Buffer.Replace(0, 0, "// ");
        _fixture.Dialogs.SaveAnswer = SaveChoice.DontSave;

        Assert.True(await _switcher.OpenAsync(Other));

        Assert.Equal(Other, _fixture.Workspace.Root);
        Assert.Empty(_fixture.TabNames);
        Assert.Equal("class A {}", _fixture.FileSystem.ReadAllText(EditorAreaFixture.PathOf("a.cs")));
    }

    [Fact]
    public async Task Open_SameFolder_KeepsTabs()
    {
        await _fixture.OpenAsync("a.cs");

        Assert.True(await _switcher.OpenAsync(EditorAreaFixture.Root + @"\"));

        Assert.Equal(["a.cs"], _fixture.TabNames);
    }

    [Fact]
    public async Task Open_MissingFolder_KeepsCurrentAndReports()
    {
        await _fixture.OpenAsync("a.cs");
        _recent.Add(@"C:\gone");

        Assert.False(await _switcher.OpenAsync(@"C:\gone"));

        Assert.Equal(EditorAreaFixture.Root, _fixture.Workspace.Root);
        Assert.Equal(["a.cs"], _fixture.TabNames);
        Assert.Empty(_recent.Items);
        Assert.Contains("не найдена", _fixture.StatusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Close_ClosesTabs_ReopeningRestoresThem()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");

        Assert.True(await _switcher.CloseAsync());
        Assert.Null(_fixture.Workspace.Root);
        Assert.Empty(_fixture.TabNames);

        Assert.True(await _switcher.OpenAsync(EditorAreaFixture.Root));
        Assert.Equal(["a.cs", "b.cs"], _fixture.TabNames);
        Assert.Equal("b.cs", _fixture.Area.Active?.Title);
    }

    // As in VS Code: a folder open in another window brings that window to the front instead of a second copy.
    [Fact]
    public async Task Open_FolderOfAnotherWindow_ActivatesItAndKeepsThisWindow()
    {
        await _fixture.OpenAsync("a.cs");
        _windows.OtherFolders.Add(Other);

        Assert.True(await _switcher.OpenAsync(Other));

        Assert.Equal([Other], _windows.Activated);
        Assert.Equal(EditorAreaFixture.Root, _fixture.Workspace.Root);
        Assert.Equal(["a.cs"], _fixture.TabNames);
    }
}
