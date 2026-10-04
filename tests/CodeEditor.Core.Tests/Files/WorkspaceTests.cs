using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Core.Tests.Files;

public sealed class WorkspaceTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Combine(Root, ".gitignore"), "*.log\nartifacts/\n")
        .AddFile(Path.Combine(Root, "src", "Program.cs"));

    private readonly ContextKeyService _context = new();
    private readonly Workspace _workspace;
    private int _changes;

    public WorkspaceTests()
    {
        _workspace = new Workspace(_fileSystem, _context, NullLogger<Workspace>.Instance);
        _workspace.Changed += (_, _) => _changes++;
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Open_SetsRootNameAndContext()
    {
        _workspace.Open(Root + @"\");

        Assert.Equal(Root, _workspace.Root);
        Assert.Equal("repo", _workspace.Name);
        Assert.Equal(true, _context.GetValue(IWorkspace.OpenContextKey));
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void Open_MissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _workspace.Open(@"C:\missing"));
        Assert.Null(_workspace.Root);
    }

    [Fact]
    public void Close_ResetsStateAndStopsWatching()
    {
        _workspace.Open(Root);

        _workspace.Close();

        Assert.Null(_workspace.Root);
        Assert.Equal(false, _context.GetValue(IWorkspace.OpenContextKey));
        Assert.True(Assert.Single(_fileSystem.Watchers).IsDisposed);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void Reopen_ReplacesWatcher()
    {
        _fileSystem.AddDirectory(@"C:\other");
        _workspace.Open(Root);

        _workspace.Open(@"C:\other");

        Assert.Equal(2, _fileSystem.Watchers.Count);
        Assert.True(_fileSystem.Watchers[0].IsDisposed);
        Assert.False(_fileSystem.Watchers[1].IsDisposed);
    }

    [Theory]
    [InlineData(@"src\Program.cs", false, false)]
    [InlineData(@"src\app.log", false, true)]
    [InlineData(@"artifacts", true, true)]
    [InlineData(@"src\bin\Debug\a.dll", false, true)]
    [InlineData(@"..\outside.cs", false, true)]
    public void IsExcluded_UsesGitIgnoreDefaultsAndRootBoundary(string relative, bool isDirectory, bool excluded)
    {
        _workspace.Open(Root);

        Assert.Equal(excluded, _workspace.IsExcluded(Path.GetFullPath(Path.Combine(Root, relative)), isDirectory));
    }

    [Fact]
    public void IsExcluded_WithoutOpenFolder_IsTrue()
    {
        Assert.True(_workspace.IsExcluded(Path.Combine(Root, "src", "Program.cs"), isDirectory: false));
    }

    [Fact]
    public void RelativePath_UsesForwardSlashes()
    {
        _workspace.Open(Root);

        Assert.Equal("src/Program.cs", _workspace.RelativePath(Path.Combine(Root, "src", "Program.cs")));
    }

    [Fact]
    public void FilesChanged_ForwardsWatcherEventsWithoutExcludedPaths()
    {
        _workspace.Open(Root);
        FileChangesEventArgs? received = null;
        _workspace.FilesChanged += (_, e) => received = e;

        _fileSystem.Watchers[0].Raise(
            new FileChange(Path.Combine(Root, "src", "New.cs"), FileChangeKind.Created),
            new FileChange(Path.Combine(Root, "debug.log"), FileChangeKind.Changed));

        var change = Assert.Single(received!.Changes);
        Assert.EndsWith("New.cs", change.Path, StringComparison.Ordinal);
    }

    // The editor folder used to be .codeeditor (before ADR 0038); opening renames it to .breeze with its data.
    [Fact]
    public void Open_RenamesLegacyDataFolder()
    {
        _fileSystem.AddFile(Path.Combine(Root, ".codeeditor", "agent", "chat-1.json"), "{}");

        _workspace.Open(Root);

        Assert.True(_fileSystem.FileExists(Path.Combine(Root, ".breeze", "agent", "chat-1.json")));
        Assert.False(_fileSystem.DirectoryExists(Path.Combine(Root, ".codeeditor")));
    }

    [Fact]
    public void Open_KeepsBothFolders_WhenNewOneExists()
    {
        _fileSystem.AddFile(Path.Combine(Root, ".codeeditor", "settings.json"), "{}");
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "settings.json"), "{}");

        _workspace.Open(Root);

        Assert.True(_fileSystem.FileExists(Path.Combine(Root, ".codeeditor", "settings.json")));
        Assert.True(_fileSystem.FileExists(Path.Combine(Root, ".breeze", "settings.json")));
    }
}
