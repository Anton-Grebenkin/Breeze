using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Documents.Commands;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Formats.Word;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Testing;
using static CodeEditor.Modules.Documents.Tests.DocumentsFixture;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Viewers read nothing until shown; Word and PowerPoint load as blocks, Excel and CSV as sheets with rows, PDF as an
/// address. A file changed on disk reloads after a pause; errors are shown while the previous content stays.
/// </summary>
public sealed class DocumentViewerTests : IDisposable
{
    private readonly DocumentsFixture _fixture = new();
    private readonly ManualTimeProvider _time = new();
    private readonly FakeSystemShell _shell = new();
    private readonly DocumentViewerProvider _provider;

    public DocumentViewerTests() =>
        _provider = new DocumentViewerProvider(new DocumentViewerContext(_fixture.Files, _fixture.Workspace, _shell, new InlineUiDispatcher(), _time, _fixture.Commands));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("a.pdf", typeof(PdfViewerViewModel))]
    [InlineData("a.DOCX", typeof(RichDocumentViewerViewModel))]
    [InlineData("a.pptx", typeof(RichDocumentViewerViewModel))]
    [InlineData("a.xlsx", typeof(SpreadsheetViewerViewModel))]
    [InlineData("a.csv", typeof(SpreadsheetViewerViewModel))]
    public void Provider_OpensDocuments_WithoutReadingThem(string name, Type expected)
    {
        Assert.True(_provider.CanOpen(name));
        using var viewer = (DocumentViewerViewModel)_provider.CreateViewer(PathOf(name));

        Assert.IsType(expected, viewer);
        Assert.False(viewer.IsLoading);
    }

    [Theory]
    [InlineData("a.cs")]
    [InlineData("a.doc")]
    [InlineData("a.txt")]
    public void Provider_LeavesOtherFilesToTheTextEditor(string name) => Assert.False(_provider.CanOpen(name));

    [Fact]
    public async Task Word_IsShownAsBlocks()
    {
        _fixture.Files.AddBytes(PathOf("a.docx"), DocxWriter.Create(MarkdownReader.Read("# Заголовок\n\nАбзац")));
        using var viewer = Open<RichDocumentViewerViewModel>("a.docx");

        await viewer.EnsureLoadedAsync();

        Assert.Equal(["Заголовок", "Абзац"], viewer.Document!.Blocks.Select(block => block.PlainText));
        Assert.Null(viewer.Error);
    }

    [Fact]
    public async Task Presentation_SaysHowManySlides()
    {
        _fixture.Files.AddBytes(PathOf("deck.pptx"), TestPresentations.TwoSlides());
        using var viewer = Open<RichDocumentViewerViewModel>("deck.pptx");

        await viewer.EnsureLoadedAsync();

        Assert.Equal("2 слайда", viewer.Summary);
    }

    [Fact]
    public async Task Workbook_ShowsSheetsAndCells_NumbersInTheUserLanguage()
    {
        var book = XlsxWriter.Create([
            new SheetInput("Первый", [[Json("\"Имя\""), Json("\"Сумма\"")], [Json("\"Иван\""), Json("1.5")]]),
            new SheetInput("Второй", [[Json("1")]])]);
        _fixture.Files.AddBytes(PathOf("book.xlsx"), book);
        using var viewer = Open<SpreadsheetViewerViewModel>("book.xlsx");

        await viewer.EnsureLoadedAsync();

        Assert.True(viewer.HasManySheets);
        var sheet = viewer.SelectedSheet!;
        Assert.Equal("Первый", sheet.Name);
        Assert.Equal(["A", "B"], sheet.Columns);
        Assert.Equal((2, "Иван", 1.5.ToString(CultureInfo.CurrentCulture)), (sheet.Rows[1].Number, sheet.Rows[1][1], sheet.Rows[1][2]));
        Assert.Equal(string.Empty, sheet.Rows[0][3]);
        Assert.Equal("2 строки, 2 столбца", viewer.Summary);
    }

    [Fact]
    public async Task Csv_IsOneSheet_AndCanBeOpenedAsText()
    {
        _fixture.Files.AddBytes(PathOf("data.csv"), "a,b\n1,2\n"u8.ToArray());
        using var viewer = Open<SpreadsheetViewerViewModel>("data.csv");

        await viewer.EnsureLoadedAsync();
        await viewer.OpenAsTextCommand.ExecuteAsync(null);

        Assert.False(viewer.HasManySheets);
        Assert.Equal("data", viewer.SelectedSheet!.Name);
        Assert.Contains((DocumentCommands.OpenAsTextId, (object?)PathOf("data.csv")), _fixture.Commands.Executed);
    }

    [Fact]
    public async Task Pdf_GetsItsAddress_AndARevisionPerLoad()
    {
        _fixture.Files.AddBytes(PathOf("a.pdf"), PdfDocumentTests.Pages("Страница", 1));
        using var viewer = Open<PdfViewerViewModel>("a.pdf");

        await viewer.EnsureLoadedAsync();
        await viewer.RefreshAsync();

        Assert.Equal(new Uri(PathOf("a.pdf")), viewer.Source);
        Assert.Equal(2, viewer.Revision);
    }

    // The agent or Word rewrote the file: the viewer reloads it on its own after a pause.
    [Fact]
    public async Task ChangedFile_IsReloadedAfterAPause()
    {
        _fixture.Files.AddBytes(PathOf("a.docx"), DocxWriter.Create(MarkdownReader.Read("Было")));
        using var viewer = Open<RichDocumentViewerViewModel>("a.docx");
        await viewer.EnsureLoadedAsync();
        var reloaded = Changed(viewer, nameof(RichDocumentViewerViewModel.Document));

        _fixture.Files.WriteAllBytesAtomic(PathOf("a.docx"), DocxWriter.Create(MarkdownReader.Read("Стало")));
        _fixture.Files.Watchers.Single().Raise(new FileChange(PathOf("a.docx"), FileChangeKind.Changed));
        Assert.False(reloaded.Task.IsCompleted, "Сразу после события файл не читается: программа ещё пишет его.");
        _time.Advance(DocumentViewerViewModel.ReloadDelay);

        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal("Стало", viewer.Document!.Blocks.Single().PlainText);
    }

    [Fact]
    public async Task BrokenFile_ShowsAnError_AndKeepsTheOldContent()
    {
        _fixture.Files.AddBytes(PathOf("a.docx"), DocxWriter.Create(MarkdownReader.Read("Текст")));
        using var viewer = Open<RichDocumentViewerViewModel>("a.docx");
        await viewer.EnsureLoadedAsync();

        _fixture.Files.AddFile(PathOf("a.docx"), "испорчен");
        await viewer.RefreshAsync();

        Assert.NotNull(viewer.Error);
        Assert.Equal("Текст", viewer.Document!.Blocks.Single().PlainText);
        Assert.False(viewer.IsLoading);
    }

    [Fact]
    public async Task BrokenXmlInside_IsShownAsAnError()
    {
        _fixture.Files.AddBytes(PathOf("broken.docx"), TestDocuments.WordWithBrokenXml());
        using var viewer = Open<RichDocumentViewerViewModel>("broken.docx");

        await viewer.EnsureLoadedAsync();

        Assert.NotNull(viewer.Error);
        Assert.Null(viewer.Document);
    }

    [Fact]
    public async Task MissingFile_IsAnError()
    {
        using var viewer = Open<PdfViewerViewModel>("nope.pdf");

        await viewer.EnsureLoadedAsync();

        Assert.Contains("Файл удалён", viewer.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenExternal_UsesTheLauncher_AndReportsFailures()
    {
        using var viewer = Open<PdfViewerViewModel>("a.pdf");

        viewer.OpenExternalCommand.Execute(null);
        _shell.LaunchFails = true;
        viewer.OpenExternalCommand.Execute(null);

        Assert.Equal([PathOf("a.pdf"), PathOf("a.pdf")], _shell.Launched);
        Assert.NotNull(viewer.Error);
    }

    [Fact]
    public async Task Disposed_Viewer_StopsWatching()
    {
        _fixture.Files.AddBytes(PathOf("a.pdf"), [0x25]);
        var viewer = Open<PdfViewerViewModel>("a.pdf");
        await viewer.EnsureLoadedAsync();

        viewer.Dispose();
        _fixture.Files.Watchers.Single().Raise(new FileChange(PathOf("a.pdf"), FileChangeKind.Changed));
        _time.Advance(DocumentViewerViewModel.ReloadDelay);

        Assert.Equal(1, viewer.Revision);
    }

    private T Open<T>(string name)
        where T : DocumentViewerViewModel => (T)_provider.CreateViewer(PathOf(name));

    private static TaskCompletionSource Changed(INotifyPropertyChanged source, string property)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == property)
            {
                changed.TrySetResult();
            }
        };
        return changed;
    }

}
