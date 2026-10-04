using CodeEditor.Modules.Explorer.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class ExplorerDropTests : IDisposable
{
    private readonly ExplorerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static string PathOf(string relativePath) => Path.Combine(ExplorerFixture.Root, relativePath);

    [Fact]
    public async Task Target_IsFolderRow_FileFolder_OrRootForEmptySpace()
    {
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("src");

        Assert.Same(_fixture.Node("docs"), _fixture.Drop.TargetOf(_fixture.Node("docs")));
        Assert.Same(_fixture.Node("src"), _fixture.Drop.TargetOf(_fixture.Node(@"src\Program.cs")));
        Assert.Same(_fixture.Tree.Root, _fixture.Drop.TargetOf(_fixture.Node("README.md")));
        Assert.Same(_fixture.Tree.Root, _fixture.Drop.TargetOf(null));
    }

    [Theory]
    [InlineData("README.md", "docs", ExplorerDropEffect.Move, ExplorerDropEffect.Move)]
    [InlineData("README.md", null, ExplorerDropEffect.Move, ExplorerDropEffect.None)]
    [InlineData("README.md", ".gitignore", ExplorerDropEffect.Copy, ExplorerDropEffect.Copy)]
    [InlineData("docs", "docs", ExplorerDropEffect.Move, ExplorerDropEffect.None)]
    [InlineData("src", @"src\Program.cs", ExplorerDropEffect.Copy, ExplorerDropEffect.None)]
    [InlineData("src", @"src\lib", ExplorerDropEffect.Copy, ExplorerDropEffect.None)]
    [InlineData("", "docs", ExplorerDropEffect.Move, ExplorerDropEffect.None)]
    public async Task Evaluate_RefusesDropsIntoItselfOrWhereItemsAre(string source, string? target, ExplorerDropEffect requested, ExplorerDropEffect expected)
    {
        _fixture.FileSystem.AddFile(PathOf(@"src\lib\util.cs"));
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("src");
        var node = target is null ? null : _fixture.Node(target);

        Assert.Equal(expected, _fixture.Drop.Evaluate([Path.TrimEndingDirectorySeparator(PathOf(source))], node, requested));
    }

    [Fact]
    public async Task MoveFile_IntoCollapsedFolder_ExpandsAndSelectsIt()
    {
        await _fixture.OpenAsync();

        await _fixture.Drop.DropAsync([PathOf("README.md")], _fixture.Node("src"), ExplorerDropEffect.Move);

        Assert.True(_fixture.FileSystem.FileExists(PathOf(@"src\README.md")));
        Assert.False(_fixture.FileSystem.FileExists(PathOf("README.md")));
        Assert.DoesNotContain(_fixture.Tree.Root.Children, node => node.Name == "README.md");
        Assert.True(_fixture.Node("src").IsExpanded);
        Assert.True(_fixture.Node(@"src\README.md").IsSelected);
        Assert.Single(_fixture.Node("src").Children, node => node.Name == "README.md");
    }

    [Fact]
    public async Task MoveFolder_KeepsItsContentForExpand()
    {
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("docs");

        await _fixture.Drop.DropAsync([PathOf("docs")], _fixture.Node("src"), ExplorerDropEffect.Move);

        Assert.True(_fixture.FileSystem.FileExists(PathOf(@"src\docs\guide.md")));
        Assert.False(_fixture.Tree.TryGet(PathOf(@"docs\guide.md"), out _));
        await _fixture.ExpandAsync(@"src\docs");
        Assert.Equal(["guide.md"], _fixture.Node(@"src\docs").Children.Select(node => node.Name));
    }

    [Fact]
    public async Task CopyIntoSameFolder_GetsCopyNames()
    {
        await _fixture.OpenAsync();

        await _fixture.Drop.DropAsync([PathOf("README.md")], null, ExplorerDropEffect.Copy);
        await _fixture.Drop.DropAsync([PathOf("README.md")], null, ExplorerDropEffect.Copy);
        await _fixture.Drop.DropAsync([PathOf(".gitignore")], null, ExplorerDropEffect.Copy);

        Assert.True(_fixture.FileSystem.FileExists(PathOf("README.md")));
        Assert.Equal("*.log", _fixture.FileSystem.ReadAllText(PathOf(".gitignore копия")));
        Assert.Contains(_fixture.Tree.Root.Children, node => node.Name == "README копия.md");
        Assert.Contains(_fixture.Tree.Root.Children, node => node.Name == "README копия 2.md");
    }

    [Fact]
    public async Task CopyFolder_CopiesContentRecursively()
    {
        await _fixture.OpenAsync();

        await _fixture.Drop.DropAsync([PathOf("src")], _fixture.Node("docs"), ExplorerDropEffect.Copy);

        Assert.True(_fixture.FileSystem.FileExists(PathOf(@"docs\src\Program.cs")));
        Assert.True(_fixture.FileSystem.DirectoryExists(PathOf(@"docs\src\bin")));
        Assert.True(_fixture.FileSystem.FileExists(PathOf(@"src\Program.cs")));
    }

    [Fact]
    public async Task FileFromWindows_IsCopiedIn()
    {
        var outside = Path.GetFullPath(@"C:\downloads\notes.txt");
        _fixture.FileSystem.AddFile(outside, "заметки");
        await _fixture.OpenAsync();

        await _fixture.Drop.DropAsync([outside], _fixture.Node("docs"), ExplorerDropEffect.Copy);

        Assert.Equal("заметки", _fixture.FileSystem.ReadAllText(PathOf(@"docs\notes.txt")));
        Assert.True(_fixture.FileSystem.FileExists(outside));
    }

    [Fact]
    public async Task NameClash_Confirmed_ReplacesViaRecycleBin()
    {
        _fixture.FileSystem.AddFile(PathOf(@"docs\README.md"), "old");
        _fixture.FileSystem.AddFile(PathOf("README.md"), "new");
        await _fixture.OpenAsync();

        await _fixture.Drop.DropAsync([PathOf("README.md")], _fixture.Node("docs"), ExplorerDropEffect.Move);

        Assert.Contains("«README.md» уже есть в «docs»", Assert.Single(_fixture.Dialogs.Confirmations), StringComparison.Ordinal);
        Assert.Equal([PathOf(@"docs\README.md")], _fixture.FileSystem.RecycledPaths);
        Assert.Equal("new", _fixture.FileSystem.ReadAllText(PathOf(@"docs\README.md")));
    }

    [Fact]
    public async Task NameClash_Declined_SkipsItem()
    {
        _fixture.FileSystem.AddFile(PathOf(@"docs\README.md"), "old");
        await _fixture.OpenAsync();
        _fixture.Dialogs.ConfirmAnswer = false;

        await _fixture.Drop.DropAsync([PathOf("README.md")], _fixture.Node("docs"), ExplorerDropEffect.Move);

        Assert.True(_fixture.FileSystem.FileExists(PathOf("README.md")));
        Assert.Equal("old", _fixture.FileSystem.ReadAllText(PathOf(@"docs\README.md")));
        Assert.Empty(_fixture.FileSystem.RecycledPaths);
    }

    [Fact]
    public async Task NameClash_WithFolderHoldingTheItem_IsNotReplaced()
    {
        _fixture.FileSystem.AddFile(PathOf(@"docs\docs\inner.md"));
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("docs");

        await _fixture.Drop.DropAsync([PathOf(@"docs\docs")], null, ExplorerDropEffect.Move);

        Assert.Empty(_fixture.Dialogs.Confirmations);
        Assert.True(_fixture.FileSystem.FileExists(PathOf(@"docs\docs\inner.md")));
        Assert.Contains("Нельзя заменить «docs»", _fixture.StatusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MovedFile_OpenTabFollows()
    {
        await _fixture.OpenAsync();
        await _fixture.Editors.OpenTextAsync(new OpenFileRequest(PathOf("README.md")));

        await _fixture.Drop.DropAsync([PathOf("README.md")], _fixture.Node("docs"), ExplorerDropEffect.Move);

        Assert.Equal(PathOf(@"docs\README.md"), Assert.Single(_fixture.Editors.Tabs).FilePath);
    }
}
