using System.Text.Json;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Excel reading: numbers distinguished only by format (date, time of day, duration, the 1904 date system of Excel for
/// Mac workbooks) and ranges that miss the sheet data.
/// </summary>
public sealed class ExcelReadingTests
{
    [Theory]
    [InlineData(0U, null, "Number")]
    [InlineData(14U, null, "Date")]
    [InlineData(21U, null, "Time")]
    [InlineData(46U, null, "Duration")]
    [InlineData(164U, "[h]:mm:ss", "Duration")]
    [InlineData(164U, "[mm]:ss", "Duration")]
    [InlineData(164U, "mm:ss", "Time")]
    [InlineData(164U, "h:mm AM/PM", "Time")]
    [InlineData(164U, "dd.mm.yyyy hh:mm", "Date")]
    [InlineData(164U, "[$-419]d mmmm yyyy", "Date")]
    [InlineData(164U, "[Magenta]0.00", "Number")]
    [InlineData(164U, "#,##0 \"дней\"", "Number")]
    [InlineData(164U, "0.00E+00", "Number")]
    [InlineData(14U, "General", "Number")]
    public void NumberFormats_AreClassified(uint id, string? code, string kind) =>
        Assert.Equal(kind, NumberFormats.Classify(id, code).ToString());

    // "[h]:mm:ss" is a 36-hour duration, not a date; dates before March 1900 account for Excel's fake 29 Feb 1900.
    [Fact]
    public void Reader_ShowsDurationsTimesAndDates()
    {
        var cells = Cells(TestDocuments.WorkbookWithNumberFormats(date1904: false));

        Assert.Equal((CellKind.Date, "36:00:00"), (cells["A1"].Kind, cells["A1"].Value));
        Assert.Equal("36:00:00", cells["A2"].Value);
        Assert.Equal("00:30:00", cells["A3"].Value);
        Assert.Equal("2024-10-03 12:00", cells["A4"].Value);
        Assert.Equal((CellKind.Number, "2.5"), (cells["A5"].Kind, cells["A5"].Value));
        Assert.Equal("1900-01-01", cells["A6"].Value);
    }

    // In the 1904 system the same date is stored as a number 1462 lower; durations are unaffected.
    [Fact]
    public void Reader_UnderstandsThe1904DateSystem()
    {
        var cells = Cells(TestDocuments.WorkbookWithNumberFormats(date1904: true));

        Assert.Equal("2028-10-04 12:00", cells["A4"].Value);
        Assert.Equal("1904-01-02", cells["A6"].Value);
        Assert.Equal("36:00:00", cells["A1"].Value);
    }

    [Theory]
    [InlineData("F1:H3")]
    [InlineData("A10:B20")]
    [InlineData("Z:Z")]
    public void SheetText_ForARangeWithoutData_SaysWhereTheDataIs(string range)
    {
        var sheet = XlsxReader.Read(XlsxWriter.Create([new SheetInput("Лист", [Row("a", "b"), Row(1, 2)])])).Sheets[0];

        var text = SheetText.Table(sheet, CellRange.TryParse(range, out var parsed) ? parsed : throw new ArgumentException(range));

        Assert.Contains("A1:B2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("|---|", text, StringComparison.Ordinal);
    }

    private static Dictionary<string, SpreadsheetCell> Cells(byte[] workbook) =>
        XlsxReader.Read(workbook).Sheets[0].Cells.ToDictionary(cell => cell.Address.ToString());

    private static JsonElement[] Row(params object[] values) => [.. values.Select(value => JsonSerializer.SerializeToElement(value))];
}
