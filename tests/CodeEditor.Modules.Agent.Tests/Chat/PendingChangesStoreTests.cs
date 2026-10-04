using CodeEditor.Core.Context;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Services.Chat;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Chat;

/// <summary>Agent edits awaiting review survive a restart and follow the open folder (ADR 0040).</summary>
public sealed class PendingChangesStoreTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");
    private static readonly string Other = Path.GetFullPath(@"C:\other");
    private static readonly string FilePath = Path.Combine(Root, "src", "a.cs");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(Path.Combine(Root, "src")).AddDirectory(Other);
    private readonly Core.Files.Workspace _workspace;

    public PendingChangesStoreTests()
    {
        _workspace = new Core.Files.Workspace(_fileSystem, new ContextKeyService(), NullLogger<Core.Files.Workspace>.Instance);
        _fileSystem.AddFile(FilePath, "edited");
        _workspace.Open(Root);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Edits_AreRestoredAfterRestart()
    {
        var (state, store) = Start();
        state.RecordWrite(FilePath, "edited", "original");
        state.RecordWrite(Path.Combine(Root, "new.cs"), "created", previousText: null);
        _fileSystem.AddFile(Path.Combine(Root, "new.cs"), "created");
        store.Dispose();

        var (restored, _) = Start();

        Assert.Equal("original", restored.Changes[FilePath]);
        Assert.Null(restored.Changes[Path.Combine(Root, "new.cs")]);
    }

    [Fact]
    public void FileBackToOriginal_IsDropped_AndEmptyListRemovesTheFile()
    {
        var (state, store) = Start();
        state.RecordWrite(FilePath, "edited", "original");
        store.Dispose();
        _fileSystem.AddFile(FilePath, "original");

        var (restored, restoredStore) = Start();
        Assert.Empty(restored.Changes);

        restored.RecordWrite(FilePath, "again", "original");
        restored.AcceptFile(FilePath);
        restoredStore.Dispose();

        Assert.False(_fileSystem.FileExists(Path.Combine(Root, ".breeze", "agent", PendingChangesStore.FileName)));
    }

    [Fact]
    public void OtherFolder_HasItsOwnList()
    {
        var (state, store) = Start();
        state.RecordWrite(FilePath, "edited", "original");

        _workspace.Open(Other);
        Assert.Empty(state.Changes);

        _workspace.Open(Root);
        Assert.Equal("original", state.Changes[FilePath]);
        store.Dispose();
    }

    private (AgentFileState State, PendingChangesStore Store) Start()
    {
        var state = new AgentFileState();
        return (state, new PendingChangesStore(state, _workspace, _fileSystem, NullLogger<PendingChangesStore>.Instance));
    }
}
