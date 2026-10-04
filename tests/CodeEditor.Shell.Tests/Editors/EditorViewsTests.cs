using CodeEditor.Core.Documents;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Shell.Tests.Editors;

/// <summary>
/// Tabs without a text document (ADR 0031): a viewer takes files that don't open as text, and a module view opens once
/// per id and is disposed on close.
/// </summary>
public sealed class EditorViewsTests : IDisposable
{
    private readonly PdfViewers _viewers = new();
    private readonly EditorAreaFixture _fixture;
    private readonly EditorViews _views;

    public EditorViewsTests()
    {
        _fixture = new EditorAreaFixture(_viewers);
        _fixture.FileSystem.AddBytes(EditorAreaFixture.PathOf("doc.pdf"), [0x25, 0x50, 0x44, 0x46]);
        _views = new EditorViews(Area);
    }

    private EditorAreaViewModel Area => _fixture.Area;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task FileWithAViewer_OpensInTheViewer_NotAsText()
    {
        var tab = await Area.OpenAsync(new OpenFileRequest(EditorAreaFixture.PathOf("doc.pdf")));

        var view = Assert.IsType<ViewTabViewModel>(tab);
        Assert.Equal(("doc.pdf", EditorAreaFixture.PathOf("doc.pdf")), (view.Title, view.FilePath));
        Assert.IsType<Viewer>(view.Editor);
        Assert.Null(Area.ActiveDocument);
        Assert.Null(await Area.OpenTextAsync(new OpenFileRequest(EditorAreaFixture.PathOf("doc.pdf"))));
        Assert.Single(Area.Tabs);
    }

    // CSV is text shown as a table: opening it as text replaces the viewer tab in place.
    [Fact]
    public async Task TextFileInAViewer_OpenedAsText_ReplacesTheViewerTab()
    {
        _fixture.FileSystem.AddFile(EditorAreaFixture.PathOf("data.csv"), "a;b\n1;2");
        await Area.OpenAsync(new OpenFileRequest(EditorAreaFixture.PathOf("data.csv")));
        await Area.OpenAsync(new OpenFileRequest(EditorAreaFixture.PathOf("doc.pdf")));

        var text = await Area.OpenTextAsync(new OpenFileRequest(EditorAreaFixture.PathOf("data.csv")));

        Assert.NotNull(text);
        Assert.Equal([text, Area.Find(EditorAreaFixture.PathOf("doc.pdf"))!], Area.Tabs);
        Assert.Same(text, Area.Active);
    }

    // A binary file without its own viewer opens in the hex viewer instead of failing, but not when opened as text.
    [Fact]
    public async Task BinaryFile_OpensInTheFallbackViewer_ButNotAsText()
    {
        using var fixture = new EditorAreaFixture(new BinaryViewers());
        var path = EditorAreaFixture.PathOf("app.bin");
        fixture.FileSystem.AddBytes(path, [0x4D, 0x5A, 0x00, 0x00, 0x01]);

        var tab = await fixture.Area.OpenAsync(new OpenFileRequest(path));

        Assert.IsType<Viewer>(Assert.IsType<ViewTabViewModel>(tab).Editor);
        await fixture.Area.CloseAsync(tab);
        Assert.Null(await fixture.Area.OpenTextAsync(new OpenFileRequest(path)));
        Assert.Empty(fixture.Area.Tabs);
    }

    [Fact]
    public void View_OpensOncePerId_AndIsDisposedOnClose()
    {
        var first = _views.Open(new EditorViewRequest("git.diff:a.cs", "a.cs (изменения)", () => new Viewer()));
        var second = _views.Open(new EditorViewRequest("git.diff:a.cs", "a.cs (изменения)", () => throw new InvalidOperationException("Создан второй раз.")));

        Assert.Same(first, second);
        Assert.Null(first.FilePath);
        _views.Close("git.diff:a.cs");

        Assert.Empty(Area.Tabs);
        Assert.True(((Viewer)first.Editor).IsDisposed);
    }

    // A tool window moved into the editor lives on: closing its tab doesn't dispose the content.
    [Fact]
    public async Task ViewWithoutOwnership_KeepsItsContent()
    {
        var content = new Viewer();
        var tab = Area.Insert(new ViewTabViewModel("toolwindow:browser", "Браузер", null, null, content, isPreview: false, ownsEditor: false));

        await Area.CloseAsync(tab);

        Assert.False(content.IsDisposed);
    }

    private sealed class PdfViewers : IFileViewerProvider
    {
        public int Priority => 10;

        public bool CanOpen(string filePath) =>
            filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

        public bool IsText(string filePath) => filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

        public object CreateViewer(string filePath) => new Viewer();
    }

    /// <summary>Like the hex viewer: no own extensions, takes binary files that failed to open as text.</summary>
    private sealed class BinaryViewers : IFileViewerProvider
    {
        public int Priority => 0;

        public bool CanOpen(string filePath) => false;

        public object CreateViewer(string filePath) => new Viewer();

        public bool OpensInsteadOfText(string filePath, DocumentOpenFailure failure) => failure == DocumentOpenFailure.Binary;
    }

    private sealed class Viewer : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
