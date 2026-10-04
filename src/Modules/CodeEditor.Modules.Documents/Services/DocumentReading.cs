using System.Globalization;
using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Pdf;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Formats.Slides;
using CodeEditor.Modules.Documents.Formats.Word;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// Document text for the model (the <c>document</c> tool): PDF by pages, Word and PowerPoint as Markdown, Excel and CSV
/// as a table with cell addresses. A damaged or encrypted file gives an error the model understands.
/// </summary>
public sealed class DocumentReading(DocumentFiles files)
{
    /// <summary>PDF pages per call; the rest come with the next call using <c>pages</c>.</summary>
    public const int MaxPdfPages = 300;

    /// <exception cref="AgentToolException">The file is missing, damaged or encrypted, or an argument is invalid.</exception>
    public string Read(DocumentPath path, string? pages, string? sheet, string? range, CancellationToken cancellationToken)
    {
        var bytes = files.Read(path);
        try
        {
            return path.Kind switch
            {
                DocumentKind.Pdf => Pdf(path, bytes, pages, cancellationToken),
                DocumentKind.Word => MarkdownWriter.Write(DocxReader.Read(bytes)),
                DocumentKind.PowerPoint => Slides(PptxReader.Read(bytes), pages),
                DocumentKind.Excel => Sheets(XlsxReader.Read(bytes), sheet, range),
                _ => Sheets(CsvReader.Read(bytes, path.Full), sheet: null, range),
            };
        }
        catch (Exception exception) when (DocumentErrors.IsReadFailure(exception))
        {
            throw new AgentToolException(Format(Strings.DocumentNotReadable, path.Relative, exception.Message));
        }
    }

    private static string Pdf(DocumentPath path, byte[] bytes, string? pages, CancellationToken cancellationToken)
    {
        var count = PdfTextReader.PageCount(bytes);
        var selected = Selection(pages, count);
        var shown = selected.Take(MaxPdfPages).ToList();
        var output = new StringBuilder(Format(Strings.PdfHeader, path.Name, count)).Append('\n');
        var texts = PdfTextReader.Read(bytes, shown, cancellationToken);
        foreach (var (number, text) in texts)
        {
            output.Append('\n').Append(Format(Strings.PageMarker, number)).Append('\n').Append(text.Length == 0 ? Strings.PageWithoutText : text).Append('\n');
        }

        if (texts.All(page => page.Text.Length == 0))
        {
            output.Append('\n').Append(Strings.PdfWithoutText).Append('\n');
        }

        if (shown.Count < selected.Count)
        {
            output.Append('\n').Append(Format(Strings.MorePages, PageSelection.Describe(shown), count, shown[^1] + 1)).Append('\n');
        }

        return output.ToString();
    }

    // Slides by number: a slide's blocks run from its start to the start of the next slide.
    private static string Slides(RichDocument presentation, string? pages)
    {
        var selected = Selection(pages, presentation.SlideCount).ToHashSet();
        var current = 0;
        var blocks = presentation.Blocks.Where(block =>
        {
            current = block.Kind == DocumentBlockKind.Slide ? block.Number : current;
            return selected.Contains(current);
        });
        return MarkdownWriter.Write(new RichDocument([.. blocks]));
    }

    private static string Sheets(Spreadsheet book, string? sheet, string? range)
    {
        var target = book.Find(sheet) ?? throw new AgentToolException(Format(Strings.SheetNotFound, sheet, string.Join(", ", book.Sheets.Select(item => item.Name))));
        CellRange? area = null;
        if (range is not null)
        {
            area = CellRange.TryParse(range, out var parsed) ? parsed : throw new AgentToolException(Format(Strings.InvalidRange, range));
        }

        var list = book.Sheets.Length > 1 ? Format(Strings.SheetList, SheetText.SheetList(book)) + "\n\n" : string.Empty;
        return list + SheetText.Table(target, area);
    }

    private static IReadOnlyList<int> Selection(string? pages, int count) =>
        PageSelection.TryParse(pages, count, out var selected) ? selected : throw new AgentToolException(Format(Strings.InvalidPages, pages, count));

    private static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
