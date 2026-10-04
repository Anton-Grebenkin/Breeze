using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class ExplorerEditorTests : IDisposable
{
    private readonly ExplorerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task NewFile_InSelectedFolder_CreatesSelectsAndOpens()
    {
        await _fixture.OpenAsync();
        _fixture.Node("src").IsSelected = true;

        await _fixture.Editor.BeginCreateAsync(isDirectory: false);
        var pending = _fixture.Editor.EditingNode!;
        pending.EditName = "Service.cs";
        Assert.True(await _fixture.Editor.CommitAsync());

        var path = Path.Combine(ExplorerFixture.Root, "src", "Service.cs");
        Assert.True(_fixture.FileSystem.FileExists(path));
        Assert.True(_fixture.Node(@"src\Service.cs").IsSelected);
        Assert.Equal([path], _fixture.OpenedFiles);
        Assert.Null(_fixture.Editor.EditingNode);
        Assert.Equal(false, _fixture.Context.GetValue("explorerEditing"));
    }

    [Fact]
    public async Task NewFolder_WithoutSelection_GoesToRoot()
    {
        await _fixture.OpenAsync();

        await _fixture.Editor.BeginCreateAsync(isDirectory: true);
        _fixture.Editor.EditingNode!.EditName = "tests";
        await _fixture.Editor.CommitAsync();

        Assert.True(_fixture.FileSystem.DirectoryExists(Path.Combine(ExplorerFixture.Root, "tests")));
        Assert.Equal(["docs", "src", "tests"], _fixture.Tree.Root.Children.Where(node => node.IsDirectory).Select(node => node.Name));
        Assert.Empty(_fixture.OpenedFiles);
    }

    [Theory]
    [InlineData("", "Введите имя")]
    [InlineData("bad:name", "недопустимо")]
    [InlineData("trailing.", "недопустимо")]
    [InlineData("README.md", "уже существует")]
    public async Task InvalidName_KeepsEditingAndExplains(string name, string message)
    {
        await _fixture.OpenAsync();
        await _fixture.Editor.BeginCreateAsync(isDirectory: false);
        _fixture.Editor.EditingNode!.EditName = name;

        Assert.False(await _fixture.Editor.CommitAsync());

        Assert.NotNull(_fixture.Editor.EditingNode);
        Assert.Contains(message, _fixture.StatusBar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelNewFile_RemovesPendingNode()
    {
        await _fixture.OpenAsync();
        var before = _fixture.Tree.Root.Children.Count;

        await _fixture.Editor.BeginCreateAsync(isDirectory: false);
        Assert.Equal(before + 1, _fixture.Tree.Root.Children.Count);
        _fixture.Editor.CancelEditing();

        Assert.Equal(before, _fixture.Tree.Root.Children.Count);
    }

    [Fact]
    public async Task Rename_MovesFileAndResorts()
    {
        await _fixture.OpenAsync();
        var readme = _fixture.Node("README.md");

        _fixture.Editor.BeginRename(readme);
        readme.EditName = "0-intro.md";
        Assert.True(await _fixture.Editor.CommitAsync());

        Assert.True(_fixture.FileSystem.FileExists(Path.Combine(ExplorerFixture.Root, "0-intro.md")));
        // Natural order, as in VS Code: a dot sorts before digits.
        Assert.Equal(["docs", "src", ".gitignore", "0-intro.md"], _fixture.Tree.Root.Children.Select(node => node.Name));
    }

    [Fact]
    public async Task Rename_OnlyCase_IsAllowed()
    {
        await _fixture.OpenAsync();
        var readme = _fixture.Node("README.md");

        _fixture.Editor.BeginRename(readme);
        readme.EditName = "readme.md";

        Assert.True(await _fixture.Editor.CommitAsync());
        Assert.Equal("readme.md", readme.Name);
    }

    [Fact]
    public async Task Rename_OpenTabFollows()
    {
        await _fixture.OpenAsync();
        await _fixture.Editors.OpenTextAsync(new OpenFileRequest(Path.Combine(ExplorerFixture.Root, "README.md")));
        var readme = _fixture.Node("README.md");

        _fixture.Editor.BeginRename(readme);
        readme.EditName = "GUIDE.md";

        Assert.True(await _fixture.Editor.CommitAsync());
        Assert.Equal(Path.Combine(ExplorerFixture.Root, "GUIDE.md"), Assert.Single(_fixture.Editors.Tabs).FilePath);
    }

    [Fact]
    public async Task Delete_Confirmed_MovesToRecycleBin()
    {
        await _fixture.OpenAsync();
        _fixture.Node("docs").IsSelected = true;

        Assert.True(_fixture.Editor.Delete());

        Assert.Equal([Path.Combine(ExplorerFixture.Root, "docs")], _fixture.FileSystem.RecycledPaths);
        Assert.DoesNotContain(_fixture.Tree.Root.Children, node => node.Name == "docs");
        Assert.Contains("папку «docs»", Assert.Single(_fixture.Dialogs.Confirmations), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_Cancelled_KeepsFile()
    {
        await _fixture.OpenAsync();
        _fixture.Dialogs.ConfirmAnswer = false;

        Assert.False(_fixture.Editor.Delete(_fixture.Node("README.md")));

        Assert.Empty(_fixture.FileSystem.RecycledPaths);
    }

    [Fact]
    public async Task CopyPaths_UseSelectedNode()
    {
        await _fixture.OpenAsync();
        await _fixture.ExpandAsync("src");
        _fixture.Node(@"src\Program.cs").IsSelected = true;

        _fixture.Editor.CopyPath(relative: true);
        Assert.Equal("src/Program.cs", _fixture.SystemShell.Clipboard);

        _fixture.Editor.CopyPath(relative: false);
        Assert.Equal(Path.Combine(ExplorerFixture.Root, "src", "Program.cs"), _fixture.SystemShell.Clipboard);
    }

    [Fact]
    public async Task Open_File_ExecutesOpenFile_Folder_Toggles()
    {
        await _fixture.OpenAsync();

        await _fixture.Explorer.OpenAsync(_fixture.Node("README.md"));
        await _fixture.Explorer.OpenAsync(_fixture.Node("docs"));

        Assert.Equal([Path.Combine(ExplorerFixture.Root, "README.md")], _fixture.OpenedFiles);
        Assert.True(_fixture.Node("docs").IsExpanded);
    }

    [Fact]
    public async Task SelectingFolder_SetsContextKey()
    {
        await _fixture.OpenAsync();

        _fixture.Node("src").IsSelected = true;

        Assert.Equal(true, _fixture.Context.GetValue("explorerResourceIsFolder"));
    }
}
