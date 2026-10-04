using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Core.Tests.Files;

public sealed class FileIndexTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Combine(Root, ".gitignore"), "*.log\nartifacts/\n")
        .AddFile(Path.Combine(Root, "README.md"))
        .AddFile(Path.Combine(Root, "src", "Program.cs"))
        .AddFile(Path.Combine(Root, "src", "Utils", "Helper.cs"))
        .AddFile(Path.Combine(Root, "build.log"))
        .AddFile(Path.Combine(Root, "artifacts", "out.dll"))
        .AddFile(Path.Combine(Root, ".git", "HEAD"))
        .AddFile(Path.Combine(Root, "node_modules", "lib", "index.js"));

    private readonly Workspace _workspace;
    private readonly FileIndex _index;

    public FileIndexTests()
    {
        _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _index = new FileIndex(_workspace, _fileSystem, NullLogger<FileIndex>.Instance);
    }

    public void Dispose()
    {
        _index.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public void NoFolder_IsEmpty()
    {
        Assert.Empty(_index.Files);
        Assert.True(_index.WhenReady.IsCompleted);
    }

    [Fact]
    public async Task Open_IndexesFilesSkippingExcluded()
    {
        _workspace.Open(Root);
        await _index.WhenReady;

        Assert.Equal(
            [".gitignore", "README.md", "src/Program.cs", "src/Utils/Helper.cs"],
            _index.Files.Select(file => file.RelativePath).Order(StringComparer.Ordinal));

        var helper = _index.Files.Single(file => file.Name == "Helper.cs");
        Assert.Equal(Path.Combine(Root, "src", "Utils", "Helper.cs"), helper.FullPath);
    }

    [Fact]
    public async Task Open_RaisesChangedWhenReady()
    {
        var changes = 0;
        _index.Changed += (_, _) => Interlocked.Increment(ref changes);

        _workspace.Open(Root);
        await _index.WhenReady;

        // One for clearing the old content, one for publishing the new.
        Assert.Equal(2, Volatile.Read(ref changes));
    }

    [Fact]
    public async Task CreatedAndDeleted_UpdateIncrementally()
    {
        _workspace.Open(Root);
        await _index.WhenReady;
        var added = Path.Combine(Root, "src", "New.cs");
        _fileSystem.AddFile(added);

        Watcher.Raise(new FileChange(added, FileChangeKind.Created), new FileChange(Path.Combine(Root, "src", "Utils"), FileChangeKind.Deleted));

        var names = _index.Files.Select(file => file.Name).ToHashSet();
        Assert.Contains("New.cs", names);
        Assert.DoesNotContain("Helper.cs", names);
    }

    [Fact]
    public async Task CreatedFolder_IsScanned()
    {
        _workspace.Open(Root);
        await _index.WhenReady;
        var folder = Path.Combine(Root, "tests");
        _fileSystem.AddFile(Path.Combine(folder, "a", "ATests.cs"));

        Watcher.Raise(new FileChange(folder, FileChangeKind.Created));

        Assert.Contains(_index.Files, file => file.RelativePath == "tests/a/ATests.cs");
    }

    [Fact]
    public async Task CreatedExcluded_IsIgnored()
    {
        _workspace.Open(Root);
        await _index.WhenReady;
        var log = Path.Combine(Root, "trace.log");
        _fileSystem.AddFile(log);

        Watcher.Raise(new FileChange(log, FileChangeKind.Created));

        Assert.DoesNotContain(_index.Files, file => file.Name == "trace.log");
    }

    [Fact]
    public async Task Rescan_RebuildsIndex()
    {
        _workspace.Open(Root);
        await _index.WhenReady;
        _fileSystem.AddFile(Path.Combine(Root, "LICENSE"));

        Watcher.RaiseRescan();
        await _index.WhenReady;

        Assert.Contains(_index.Files, file => file.Name == "LICENSE");
    }

    [Fact]
    public async Task Close_ClearsIndex()
    {
        _workspace.Open(Root);
        await _index.WhenReady;

        _workspace.Close();

        Assert.Empty(_index.Files);
    }

    private FakeFileWatcher Watcher => _fileSystem.Watchers[^1];
}
