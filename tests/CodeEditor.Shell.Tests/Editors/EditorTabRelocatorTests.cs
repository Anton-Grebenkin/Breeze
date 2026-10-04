using CodeEditor.Shell.Editors;

namespace CodeEditor.Shell.Tests.Editors;

public sealed class EditorTabRelocatorTests : IDisposable
{
    private readonly EditorAreaFixture _fixture = new();

    private EditorAreaViewModel Area => _fixture.Area;

    private EditorTabRelocator Relocator => new(Area);

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task MovedFile_TabReopensInPlace()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        await _fixture.OpenAsync("c.cs");
        var target = EditorAreaFixture.PathOf(@"lib\b.cs");
        Move("b.cs", target);

        await Relocator.FollowAsync(EditorAreaFixture.PathOf("b.cs"), target);

        Assert.Equal(["a.cs", "b.cs", "c.cs"], _fixture.TabNames);
        Assert.Equal(target, Area.Tabs[1].FilePath);
        Assert.False(_fixture.Documents.TryGet(EditorAreaFixture.PathOf("b.cs"), out _));
        Assert.Equal("c.cs", Area.Active?.Title);
    }

    [Fact]
    public async Task MovedActiveFile_StaysActive()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        var target = EditorAreaFixture.PathOf("renamed.cs");
        Move("b.cs", target);

        await Relocator.FollowAsync(EditorAreaFixture.PathOf("b.cs"), target);

        Assert.Equal(["a.cs", "renamed.cs"], _fixture.TabNames);
        Assert.Equal(target, Area.Active?.FilePath);
    }

    [Fact]
    public async Task UnsavedEdits_CarryOverWithoutSavePrompt()
    {
        var tab = await _fixture.OpenAsync("a.cs");
        tab.Document.Buffer.Replace(0, 0, "// edit\n");
        var target = EditorAreaFixture.PathOf(@"lib\a.cs");
        Move("a.cs", target);

        await Relocator.FollowAsync(EditorAreaFixture.PathOf("a.cs"), target);

        var moved = Assert.IsType<EditorTabViewModel>(Assert.Single(Area.Tabs));
        Assert.Equal("// edit\nclass A {}", moved.Document.Buffer.GetText());
        Assert.True(moved.IsDirty);
        Assert.Empty(_fixture.Dialogs.SaveQuestions);
        Assert.Equal("class A {}", _fixture.FileSystem.ReadAllText(target));
    }

    [Fact]
    public async Task MovedFolder_TabsInsideFollow()
    {
        _fixture.FileSystem.AddFile(EditorAreaFixture.PathOf(@"src\d.cs"), "class D {}");
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync(@"src\d.cs", preview: true);
        var target = EditorAreaFixture.PathOf(@"lib\src");
        Move("src", target);

        await Relocator.FollowAsync(EditorAreaFixture.PathOf("src"), target);

        Assert.Equal(["a.cs", "d.cs"], _fixture.TabNames);
        Assert.Equal(Path.Combine(target, "d.cs"), Area.Tabs[1].FilePath);
        Assert.True(Area.Tabs[1].IsPreview);
    }

    [Fact]
    public async Task FileWithFolderNameAsPrefix_IsUntouched()
    {
        var tab = await _fixture.OpenAsync("b.cs");
        _fixture.FileSystem.AddDirectory(EditorAreaFixture.PathOf("b"));
        Move("b", EditorAreaFixture.PathOf("x"));

        await Relocator.FollowAsync(EditorAreaFixture.PathOf("b"), EditorAreaFixture.PathOf("x"));

        Assert.Same(tab, Assert.Single(Area.Tabs));
    }

    private void Move(string name, string target)
    {
        _fixture.FileSystem.AddDirectory(Path.GetDirectoryName(target)!);
        _fixture.FileSystem.Move(EditorAreaFixture.PathOf(name), target);
    }
}
