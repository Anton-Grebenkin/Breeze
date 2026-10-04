using System.Text;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Tests.Editors;

public sealed class EditorAreaViewModelTests : IDisposable
{
    private readonly EditorAreaFixture _fixture = new();

    private EditorAreaViewModel Area => _fixture.Area;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Open_CreatesActiveTab_OnlyOnce()
    {
        var first = await _fixture.OpenAsync("a.cs");
        var second = await _fixture.OpenAsync("a.cs");

        Assert.Same(first, second);
        Assert.Same(first, Area.Active);
        Assert.True(first.IsActive);
        Assert.Equal(true, _fixture.Context.GetValue(EditorAreaViewModel.EditorOpenContextKey));
    }

    [Fact]
    public async Task ConcurrentOpenOfSameFile_GivesOneTab_PinnedByLaterRequest()
    {
        // Explorer double click: a preview open immediately followed by a normal open while the file is still loading.
        var preview = _fixture.OpenAsync("a.cs", preview: true);
        var pinned = _fixture.OpenAsync("a.cs");

        var tabs = await Task.WhenAll(preview, pinned);

        Assert.Same(tabs[0], tabs[1]);
        Assert.Single(Area.Tabs);
        Assert.False(tabs[0].IsPreview);
    }

    [Fact]
    public async Task Open_InsertsAfterActiveTab()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        Area.Activate(Area.Tabs[0]);

        await _fixture.OpenAsync("c.cs");

        Assert.Equal(["a.cs", "c.cs", "b.cs"], _fixture.TabNames);
    }

    [Fact]
    public async Task Preview_IsReplacedByNextPreview_ButNotAfterEdit()
    {
        await _fixture.OpenAsync("a.cs", preview: true);
        await _fixture.OpenAsync("b.cs", preview: true);
        Assert.Equal(["b.cs"], _fixture.TabNames);

        Area.ActiveDocument!.Buffer.Replace(0, 0, "// ");
        Assert.False(Area.Active!.IsPreview);

        await _fixture.OpenAsync("c.cs", preview: true);
        Assert.Equal(["b.cs", "c.cs"], _fixture.TabNames);
    }

    [Fact]
    public async Task OpeningPreviewAgainWithoutPreview_PinsIt()
    {
        var tab = await _fixture.OpenAsync("a.cs", preview: true);

        await _fixture.OpenAsync("a.cs");

        Assert.False(tab.IsPreview);
    }

    [Fact]
    public async Task BinaryFile_ShowsMessageWithoutTab()
    {
        Assert.Null(await Area.OpenAsync(new OpenFileRequest(EditorAreaFixture.PathOf("app.dll"))));

        Assert.Empty(Area.Tabs);
        Assert.Contains("двоичный", _fixture.StatusBar.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SaveChoice.Save, true, true)]
    [InlineData(SaveChoice.DontSave, true, false)]
    [InlineData(SaveChoice.Cancel, false, false)]
    public async Task CloseDirty_FollowsAnswer(SaveChoice answer, bool closed, bool saved)
    {
        var tab = await _fixture.OpenAsync("a.cs");
        tab.Document.Buffer.Replace(0, 0, "// ");
        _fixture.Dialogs.SaveAnswer = answer;

        Assert.Equal(closed, await Area.CloseAsync(tab));

        Assert.Equal(closed, Area.Tabs.Count == 0);
        Assert.Equal(saved, Encoding.UTF8.GetString(_fixture.FileSystem.ReadAllBytes(EditorAreaFixture.PathOf("a.cs"))).StartsWith("// ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CloseAll_AsksOnceForAllDirtyFiles()
    {
        (await _fixture.OpenAsync("a.cs")).Document.Buffer.Replace(0, 0, "1");
        (await _fixture.OpenAsync("b.cs")).Document.Buffer.Replace(0, 0, "2");
        await _fixture.OpenAsync("c.cs");
        _fixture.Dialogs.SaveAnswer = SaveChoice.DontSave;

        Assert.True(await Area.CloseAllAsync());

        Assert.Equal(["a.cs", "b.cs"], Assert.Single(_fixture.Dialogs.SaveQuestions));
        Assert.Empty(Area.Tabs);
    }

    [Fact]
    public async Task Close_ActivatesPreviouslyUsedTab()
    {
        var a = await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        await _fixture.OpenAsync("c.cs");
        Area.Activate(a);
        Area.Activate(Area.Tabs.Single(tab => tab.Title == "c.cs"));

        await Area.CloseAsync(null);

        Assert.Same(a, Area.Active);
    }

    [Fact]
    public async Task ReopenClosed_RestoresLastClosedTab()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        await Area.CloseAsync(null);

        await Area.ReopenClosedAsync();

        Assert.Equal("b.cs", Area.Active?.Title);
        Assert.Empty(Area.RecentlyClosed);
    }

    [Fact]
    public async Task Navigation_WrapsAndSwitchesToRecent()
    {
        var a = await _fixture.OpenAsync("a.cs");
        var b = await _fixture.OpenAsync("b.cs");

        Area.ActivateNeighbor(1);
        Assert.Same(a, Area.Active);

        Area.ActivatePreviousRecent();
        Assert.Same(b, Area.Active);
    }

    [Fact]
    public async Task Shutdown_CancelledByUser_KeepsDirtyTabs()
    {
        (await _fixture.OpenAsync("a.cs")).Document.Buffer.Replace(0, 0, "1");
        _fixture.Dialogs.SaveAnswer = SaveChoice.Cancel;

        Assert.False(await Area.CanShutdownAsync());
        Assert.Single(Area.Tabs);
    }

    [Fact]
    public async Task Save_OverExternalChange_AsksToOverwrite()
    {
        var tab = await _fixture.OpenAsync("a.cs");
        tab.Document.Buffer.Replace(0, 0, "// ");
        _fixture.FileSystem.Touch(EditorAreaFixture.PathOf("a.cs"));
        _fixture.FileSystem.Watchers[0].Raise(new Core.Files.FileChange(EditorAreaFixture.PathOf("a.cs"), Core.Files.FileChangeKind.Changed));
        _fixture.Dialogs.ConfirmAnswer = false;

        await Area.SaveActiveAsync();

        Assert.True(tab.Document.IsDirty);
        Assert.Contains("изменён на диске", Assert.Single(_fixture.Dialogs.Confirmations), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveDirty_UpdatesContextKey()
    {
        var tab = await _fixture.OpenAsync("a.cs");

        tab.Document.Buffer.Replace(0, 0, "x");

        Assert.Equal(true, _fixture.Context.GetValue(EditorAreaViewModel.ActiveDirtyContextKey));
    }
}
