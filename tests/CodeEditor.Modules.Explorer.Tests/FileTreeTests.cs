using CodeEditor.Core.Files;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class FileTreeTests : IDisposable
{
    private readonly ExplorerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Root_ListsFoldersFirstAndHidesExcluded()
    {
        await _fixture.OpenAsync();

        Assert.Equal(["docs", "src", ".gitignore", "README.md"], _fixture.Tree.Root.Children.Select(node => node.Name));
    }

    [Fact]
    public async Task Folder_LoadsLazilyWithNaturalOrder()
    {
        await _fixture.OpenAsync();
        var src = _fixture.Node("src");
        Assert.False(src.IsChildrenLoaded);
        Assert.True(Assert.Single(src.Children).IsPlaceholder);

        await _fixture.ExpandAsync("src");

        Assert.Equal(["file2.cs", "file10.cs", "Program.cs"], src.Children.Select(node => node.Name));
    }

    [Fact]
    public async Task CreatedFile_AppearsInLoadedFolderInOrder()
    {
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("src");
        var path = Path.Combine(ExplorerFixture.Root, "src", "file3.cs");
        _fixture.FileSystem.AddFile(path);

        _fixture.FileSystem.Watchers[0].Raise(new FileChange(path, FileChangeKind.Created));

        Assert.Equal(["file2.cs", "file3.cs", "file10.cs", "Program.cs"], _fixture.Node("src").Children.Select(node => node.Name));
    }

    [Fact]
    public async Task CreatedFile_InUnloadedFolder_IsIgnoredUntilExpanded()
    {
        await _fixture.OpenAsync();
        var path = Path.Combine(ExplorerFixture.Root, "docs", "new.md");
        _fixture.FileSystem.AddFile(path);

        _fixture.FileSystem.Watchers[0].Raise(new FileChange(path, FileChangeKind.Created));

        Assert.False(_fixture.Tree.TryGet(path, out _));
    }

    [Fact]
    public async Task DeletedFolder_IsRemovedWithItsChildren()
    {
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("src");
        var src = Path.Combine(ExplorerFixture.Root, "src");

        _fixture.FileSystem.Watchers[0].Raise(new FileChange(src, FileChangeKind.Deleted));

        Assert.DoesNotContain(_fixture.Tree.Root.Children, node => node.Name == "src");
        Assert.False(_fixture.Tree.TryGet(Path.Combine(src, "Program.cs"), out _));
    }

    [Fact]
    public async Task Rescan_RereadsExpandedFolders_KeepingTheirState()
    {
        await _fixture.OpenAsync();
        var src = await _fixture.ExpandAsync("src");
        _fixture.FileSystem.AddFile(Path.Combine(ExplorerFixture.Root, "src", "Late.cs"));

        await _fixture.Tree.ApplyChangesAsync(new FileChangesEventArgs([], requiresRescan: true));

        Assert.Same(src, _fixture.Node("src"));
        Assert.True(src.IsExpanded);
        Assert.Contains(src.Children, node => node.Name == "Late.cs");
    }

    [Fact]
    public async Task CollapseAll_CollapsesExpandedFolders()
    {
        await _fixture.OpenAsync();
        var src = await _fixture.ExpandAsync("src");

        _fixture.Explorer.CollapseAll();

        Assert.False(src.IsExpanded);
    }

    [Fact]
    public async Task ClosingWorkspace_ClearsTree()
    {
        await _fixture.OpenAsync();

        _fixture.Workspace.Close();

        Assert.False(_fixture.Explorer.HasWorkspace);
        Assert.Null(_fixture.Explorer.Tree);
    }
}
