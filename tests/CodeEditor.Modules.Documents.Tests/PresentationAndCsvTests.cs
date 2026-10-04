using System.Text;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Formats.Slides;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// PowerPoint: slides with titles, leveled bullets, tables and notes; the layout's slide number is not text.
/// CSV: delimiter detection from lines, quoted fields, Windows-1251 encoding.
/// </summary>
public sealed class PresentationAndCsvTests
{
    [Fact]
    public void Presentation_ReadsSlidesTitlesBulletsTablesAndNotes()
    {
        var markdown = MarkdownWriter.Write(PptxReader.Read(TestPresentations.TwoSlides()));

        Assert.Equal(
            "## Слайд 1: Итоги года\n\n- Выручка\n  - **Рост на 12 %**\n\n> Заметки: Сказать про филиалы\n\n## Слайд 2: Планы\n\n| Квартал | План |\n| --- | --- |\n| I | 100 |\n",
            markdown);
    }

    [Fact]
    public void Presentation_CountsSlides() => Assert.Equal(2, PptxReader.Read(TestPresentations.TwoSlides()).SlideCount);

    [Theory]
    [InlineData("a,b,c\n1,2,3\n", ',')]
    [InlineData("Имя;Сумма\nИван;1,5\nПётр;2,75\n", ';')]
    [InlineData("a\tb\n1\t2\n", '\t')]
    [InlineData("одна колонка\nзначение\n", ',')]
    public void Csv_DetectsTheDelimiter(string text, char expected) => Assert.Equal(expected, CsvReader.Detect(text));

    [Fact]
    public void Csv_QuotedFields_KeepDelimitersQuotesAndLineBreaks()
    {
        var text = "Название,Описание\n\"Стол, круглый\",\"Сказал \"\"да\"\"\nи ушёл\"\nСтул,\n";

        var cells = CsvReader.Read(Encoding.UTF8.GetBytes(text), "мебель.csv").Sheets[0].Cells.ToDictionary(cell => cell.Address.ToString());

        Assert.Equal("Стол, круглый", cells["A2"].Value);
        Assert.Equal("Сказал \"да\"\nи ушёл", cells["B2"].Value);
        Assert.Equal("Стул", cells["A3"].Value);
        Assert.False(cells.ContainsKey("B3"));
    }

    [Fact]
    public void Csv_WindowsLineBreaksInsideQuotes_BecomeLineFeeds()
    {
        var cells = CsvReader.Read(Encoding.UTF8.GetBytes("a,b\r\n\"первая\r\nвторая\",x\r\n"), "data.csv").Sheets[0].Cells;

        Assert.Contains(cells, cell => cell.Value == "первая\nвторая");
        Assert.Equal(2, cells.Max(cell => cell.Row));
    }

    [Fact]
    public void Csv_InWindows1251_IsDecoded()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1251).GetBytes("Город;Население\nМосква;13000000\n");

        var book = CsvReader.Read(bytes, "города.csv");

        Assert.Equal("города", book.Sheets[0].Name);
        Assert.Contains(book.Sheets[0].Cells, cell => cell.Value == "Москва");
    }

    [Fact]
    public void Tsv_UsesTabs() =>
        Assert.Equal(2, CsvReader.Read("a,b\tc"u8.ToArray(), "data.tsv").Sheets[0].Cells.Length);

    [Fact]
    public void Csv_RespectsRowLimit() =>
        Assert.Equal(2, CsvReader.Read("1\n2\n3\n4\n"u8.ToArray(), "data.csv", maxRows: 2).Sheets[0].RowCount);

    [Fact]
    public void Csv_Binary_IsInvalidData() =>
        Assert.Throws<InvalidDataException>(() => CsvReader.Read([0x00, 0x01, 0x02, 0x00], "data.csv"));

    [Fact]
    public void Kinds_ByExtension()
    {
        Assert.Equal(DocumentKind.Word, DocumentKinds.Of(@"C:\a\Отчёт.DOCX"));
        Assert.Equal(DocumentKind.Csv, DocumentKinds.Of("data.tsv"));
        Assert.Equal(DocumentKind.None, DocumentKinds.Of("old.doc"));
    }
}
