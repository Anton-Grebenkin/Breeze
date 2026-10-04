using CodeEditor.Core.Storage;
using CodeEditor.Shell.Session;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Session;

public sealed class JsonSessionStoreTests
{
    private static readonly UserDataPaths Paths = new(@"C:\data");

    private readonly FakeFileSystem _fileSystem = new();
    private readonly JsonSessionStore _store;

    public JsonSessionStoreTests() => _store = new JsonSessionStore(_fileSystem, Paths, NullLogger<JsonSessionStore>.Instance);

    [Fact]
    public void FolderTabs_RoundTrip_PerFolder()
    {
        var repo = new FolderSession { Folder = @"C:\repo", Tabs = [new(@"C:\repo\a.cs", IsPreview: true, Group: 1)], ActiveTab = @"C:\repo\a.cs" };
        var other = new FolderSession { Folder = @"C:\other", Tabs = [new(@"C:\other\x.cs")] };

        _store.SaveFolder(repo);
        _store.SaveFolder(other);

        Assert.Equal(repo.Tabs, _store.LoadFolder(@"C:\repo")!.Tabs);
        Assert.Equal(repo.ActiveTab, _store.LoadFolder(@"C:\repo")!.ActiveTab);
        Assert.Equal(other.Tabs, _store.LoadFolder(@"C:\other")!.Tabs);
        Assert.Null(_store.LoadFolder(@"C:\never"));
    }

    // Windows paths: case and a trailing separator don't make another folder.
    [Fact]
    public void FolderKey_IgnoresCaseAndTrailingSeparator() =>
        Assert.Equal(JsonSessionStore.FolderKey(@"C:\Repo\"), JsonSessionStore.FolderKey(@"c:\repo"));

    [Fact]
    public void CorruptFolderFile_ActsAsNone()
    {
        _store.SaveFolder(new FolderSession { Folder = @"C:\repo" });
        var file = Path.Combine(Paths.File(JsonSessionStore.FoldersDirectory), JsonSessionStore.FolderKey(@"C:\repo") + ".json");
        _fileSystem.AddFile(file, "{ not json");

        Assert.Null(_store.LoadFolder(@"C:\repo"));
    }
}
