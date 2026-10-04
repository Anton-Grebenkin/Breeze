using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Pdf;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// PDF: creating from Markdown with Windows fonts (Cyrillic reads back), text per page, merging files and extracting
/// pages without re-layout, a clear error for a non-PDF file.
/// </summary>
public sealed class PdfDocumentTests
{
    [Fact]
    public void Markdown_WithCyrillic_ReadsBackAsText()
    {
        var bytes = PdfWriter.Create(MarkdownReader.Read("""
            # Акт сверки

            Сумма к оплате: **1 500 ₽**. Ёлка, щука, ЪЬЭЮЯ.

            - первый пункт
            - второй пункт

            | Наименование | Количество |
            | --- | --- |
            | Бумага | 10 |

            ```
            var x = 1;
            ```
            """));

        var text = PdfTextReader.Read(bytes, [1], TestContext.Current.CancellationToken).Single().Text;

        Assert.Contains("Акт сверки", text, StringComparison.Ordinal);
        Assert.Contains("Ёлка, щука, ЪЬЭЮЯ.", text, StringComparison.Ordinal);
        Assert.Contains("первый пункт", text, StringComparison.Ordinal);
        Assert.Contains("Бумага", text, StringComparison.Ordinal);
        Assert.Contains("var", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Combine_TakesPagesInOrder()
    {
        var first = Pages("Первый", 2);
        var second = Pages("Второй", 3);

        var merged = PdfPageTools.Combine([(first, [1, 2]), (second, [3, 1])]);

        Assert.Equal(4, PdfPageTools.PageCount(merged));
        var texts = PdfTextReader.Read(merged, [1, 2, 3, 4], TestContext.Current.CancellationToken).Select(page => page.Text).ToList();
        Assert.Contains("Первый 1", texts[0], StringComparison.Ordinal);
        Assert.Contains("Первый 2", texts[1], StringComparison.Ordinal);
        Assert.Contains("Второй 3", texts[2], StringComparison.Ordinal);
        Assert.Contains("Второй 1", texts[3], StringComparison.Ordinal);
    }

    [Fact]
    public void NotAPdf_IsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => PdfTextReader.PageCount("не PDF"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => PdfPageTools.PageCount("не PDF"u8.ToArray()));
    }

    /// <summary>PDF with a "{name} {page number}" heading on every page.</summary>
    internal static byte[] Pages(string name, int count) =>
        PdfPageTools.Combine([.. Enumerable.Range(1, count).Select(page => (PdfWriter.Create(MarkdownReader.Read($"# {name} {page}")), (IReadOnlyList<int>)[1]))]);
}
