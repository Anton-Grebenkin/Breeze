using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace CodeEditor.UI.Tests.Editor;

/// <summary>
/// Document viewers on the real window (ADR 0034): Word as text, Excel as a grid, PDF as a WebView2 page; the palette
/// over a PDF tab isn't covered by the page (ADR 0031).
/// </summary>
public sealed class DocumentViewerTests : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);

    private readonly string _folder;

    public DocumentViewerTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "docs")).FullName;
        SampleDocuments.WriteDocx(Path.Combine(_folder, "report.docx"), "Привет из Word");
        SampleDocuments.WriteXlsx(Path.Combine(_folder, "sales.xlsx"), "Продажи", ["Город", "Сумма"], ["Казань", "42"]);
        SampleDocuments.WritePdf(Path.Combine(_folder, "note.pdf"), "Hello PDF");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void WordExcelAndPdf_OpenInViewers()
    {
        using var session = AppSession.WithArguments(_folder);

        session.WaitFor("Explorer.Node.report.docx").GuardedDoubleClick();
        var viewer = session.WaitFor("RichDocumentViewer.Text");
        Assert.True(Retry.WhileFalse(() => DocumentText(viewer).Contains("Привет из Word", StringComparison.Ordinal), LoadTimeout).Result,
            "Текст документа Word не показан.");

        session.WaitFor("Explorer.Node.sales.xlsx").GuardedDoubleClick();
        var grid = session.WaitFor("SpreadsheetViewer.Grid");
        Assert.True(Retry.WhileNull(() => grid.FindFirstDescendant(condition => condition.ByName("Казань")), LoadTimeout).Success, "Ячейки листа не показаны.");
        session.SaveScreenshot("documents-xlsx");

        session.WaitFor("Explorer.Node.note.pdf").GuardedDoubleClick();
        session.WaitFor("PdfViewer");
        Assert.Null(session.TryFind("PdfViewer.Unavailable"));
        Assert.Null(session.TryFind("DocumentViewer.Error"));
        PaletteAirspace.AssertPaletteIsNotCovered(session);
        session.SaveScreenshot("documents-pdf");
    }

    // The text lives in a nested Document element with the Text pattern, which appears only after loading.
    private static string DocumentText(AutomationElement viewer) =>
        viewer.FindFirstDescendant(condition => condition.ByControlType(ControlType.Document)) is { } document && document.Patterns.Text.IsSupported
            ? document.Patterns.Text.Pattern.DocumentRange.GetText(-1)
            : string.Empty;

}
