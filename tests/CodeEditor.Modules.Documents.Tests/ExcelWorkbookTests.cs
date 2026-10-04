using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Formats.Excel;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Excel: a new workbook from JSON rows (values, formulas, bold frozen header, column widths), reading an Excel-made
/// workbook (shared strings, dates, shared formulas), in-place cell writes, adding a sheet and renaming with references.
/// </summary>
public sealed class ExcelWorkbookTests
{
    private static readonly SheetInput Prices = new("Цены",
    [
        Row("Товар", "Кол-во", "Цена", "Сумма"),
        Row("Яблоки", 3, 1.5, "=B2*C2"),
        Row("Груши", 2, 2.25, "=B3*C3"),
    ]);

    [Fact]
    public void Created_Workbook_ReadsBackWithFormulas()
    {
        var book = XlsxReader.Read(XlsxWriter.Create([Prices]));

        var sheet = Assert.Single(book.Sheets);
        Assert.Equal("Цены", sheet.Name);
        Assert.Equal(new CellRange(new CellAddress(1, 1), new CellAddress(3, 4)), sheet.UsedRange);
        var cells = sheet.Cells.ToDictionary(cell => cell.Address.ToString());
        Assert.Equal((CellKind.Text, "Яблоки"), (cells["A2"].Kind, cells["A2"].Value));
        Assert.Equal((CellKind.Number, "1.5"), (cells["C2"].Kind, cells["C2"].Value));
        Assert.Equal("B2*C2", cells["D2"].Formula);
    }

    [Fact]
    public void Created_Workbook_HasBoldFrozenHeaderAndWidths()
    {
        using var document = SpreadsheetDocument.Open(new MemoryStream(XlsxWriter.Create([Prices])), false);
        var worksheet = document.WorkbookPart!.WorksheetParts.Single().Worksheet!;

        var header = worksheet.Descendants<Cell>().First(cell => cell.CellReference == "A1");
        Assert.Equal(XlsxStyles.BoldFormat, header.StyleIndex?.Value);
        Assert.NotNull(worksheet.Descendants<Pane>().SingleOrDefault(pane => pane.TopLeftCell == "A2"));
        var widths = worksheet.Descendants<Column>().Select(column => column.Width!.Value).ToList();
        Assert.Equal(4, widths.Count);
        Assert.True(widths[0] >= "Яблоки".Length, "Ширина столбца — по самому длинному значению.");
        Assert.True(document.WorkbookPart.Workbook!.CalculationProperties!.FullCalculationOnLoad!.Value);
    }

    [Fact]
    public void Reader_UnderstandsAWorkbookFromExcel()
    {
        var book = XlsxReader.Read(TestDocuments.ExcelLikeWorkbook());

        var cells = book.Sheets[0].Cells.ToDictionary(cell => cell.Address.ToString());
        Assert.Equal("Число", cells["A1"].Value);
        Assert.Equal("Двойное", cells["B1"].Value);
        Assert.Equal((CellKind.Date, "2024-10-03"), (cells["C1"].Kind, cells["C1"].Value));
        Assert.Equal((CellKind.Boolean, "TRUE"), (cells["C2"].Kind, cells["C2"].Value));
        Assert.Equal((CellKind.Error, "#DIV/0!"), (cells["C3"].Kind, cells["C3"].Value));
        Assert.Equal(["A2*2", "A3*2", "A4*2"], [cells["B2"].Formula, cells["B3"].Formula, cells["B4"].Formula]);
        Assert.Equal("6", cells["B4"].Value);
    }

    [Fact]
    public void SetCells_ChangesOnlyTheGivenCells()
    {
        var bytes = XlsxWriter.Create([Prices]);

        var changed = XlsxEditor.SetCells(bytes, "цены", [(Address("B3"), Json("5")), (Address("E1"), Json("\"Комментарий\"")), (Address("A3"), Json("null"))]);

        var cells = XlsxReader.Read(changed).Sheets[0].Cells.ToDictionary(cell => cell.Address.ToString());
        Assert.Equal("5", cells["B3"].Value);
        Assert.Equal("Комментарий", cells["E1"].Value);
        Assert.False(cells.ContainsKey("A3"), "null очищает ячейку.");
        Assert.Equal("B3*C3", cells["D3"].Formula);
        Assert.Equal("Яблоки", cells["A2"].Value);
    }

    // Values follow Excel input rules: the model sends strings as a person would type them.
    [Theory]
    [InlineData("\"12.5\"", CellKind.Number, "12.5")]
    [InlineData("\"-3e2\"", CellKind.Number, "-300")]
    [InlineData("\"00123\"", CellKind.Text, "00123")]
    [InlineData("\"'0042\"", CellKind.Text, "0042")]
    [InlineData("\"1,5\"", CellKind.Text, "1,5")]
    [InlineData("\"TRUE\"", CellKind.Boolean, "TRUE")]
    [InlineData("false", CellKind.Boolean, "FALSE")]
    [InlineData("7", CellKind.Number, "7")]
    public void Values_AreTypedAsInExcel(string json, CellKind kind, string value)
    {
        var changed = XlsxEditor.SetCells(XlsxWriter.Create([Prices]), null, [(Address("F1"), Json(json))]);

        var cell = XlsxReader.Read(changed).Sheets[0].Cells.Single(item => item.Address.ToString() == "F1");
        Assert.Equal((kind, value), (cell.Kind, cell.Value));
    }

    // Excel would drop these formulas on open and offer to repair the file; the model learns about it right away.
    [Theory]
    [InlineData("\"=SUM(A1;A2)\"")]
    [InlineData("\"=СУММ(A1,A2)\"")]
    [InlineData("\"=SUM(A1,A2\"")]
    [InlineData("\"==========\"")]
    [InlineData("\"=IF(A1=\\\"x,1,2)\"")]
    public void BrokenFormulas_AreRefused(string json) =>
        Assert.Throws<AgentToolException>(() => XlsxEditor.SetCells(XlsxWriter.Create([Prices]), null, [(Address("F1"), Json(json))]));

    [Theory]
    [InlineData("\"=SUM({1,2;3,4})\"", "SUM({1,2;3,4})")]
    [InlineData("\"=IF(A1=\\\"a;b\\\",1,2)\"", "IF(A1=\"a;b\",1,2)")]
    [InlineData("\"=SUM('Лист 1'!A1:A3)\"", "SUM('Лист 1'!A1:A3)")]
    public void ValidFormulas_AreWritten(string json, string formula)
    {
        var changed = XlsxEditor.SetCells(XlsxWriter.Create([Prices]), null, [(Address("F1"), Json(json))]);

        Assert.Equal(formula, XlsxReader.Read(changed).Sheets[0].Cells.Single(cell => cell.Address.ToString() == "F1").Formula);
    }

    // A number outside the double range becomes text, not "Infinity", which would corrupt the cell.
    [Fact]
    public void HugeNumber_IsWrittenAsText()
    {
        var changed = XlsxEditor.SetCells(XlsxWriter.Create([Prices]), null, [(Address("F1"), Json("1e400")), (Address("G1"), Json("\"1e400\""))]);

        var cells = XlsxReader.Read(changed).Sheets[0].Cells.ToDictionary(cell => cell.Address.ToString());
        Assert.Equal((CellKind.Text, "1e400"), (cells["F1"].Kind, cells["F1"].Value));
        Assert.Equal(CellKind.Text, cells["G1"].Kind);
    }

    // A table header is both a cell and a column name: text changes both, anything else is an error for the model.
    [Fact]
    public void TableHeader_TakesTextAndRenamesTheColumn()
    {
        var changed = XlsxEditor.SetCells(TestDocuments.WorkbookWithHiddenSheetAndTable(), "Продажи", [(Address("B1"), Json("\"Итого\""))]);

        using var document = SpreadsheetDocument.Open(new MemoryStream(changed), false);
        var columns = document.WorkbookPart!.WorksheetParts.SelectMany(part => part.TableDefinitionParts).Single().Table!.TableColumns!.Elements<TableColumn>();
        Assert.Equal(["Имя", "Итого"], columns.Select(column => column.Name!.Value!));
        Assert.Equal("Итого", XlsxReader.Read(changed).Find("Продажи")!.Cells.Single(cell => cell.Address.ToString() == "B1").Value);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("\"=A2\"")]
    [InlineData("\"\"")]
    [InlineData("\"Имя\"")]
    public void TableHeader_RefusesNonTextAndDuplicates(string json) =>
        Assert.Throws<AgentToolException>(() => XlsxEditor.SetCells(TestDocuments.WorkbookWithHiddenSheetAndTable(), "Продажи", [(Address("B1"), Json(json))]));

    // Without a sheet name the first visible sheet is used: the user can't see the hidden settings sheet.
    [Fact]
    public void WithoutSheetName_TheFirstVisibleSheetIsUsed()
    {
        var bytes = TestDocuments.WorkbookWithHiddenSheetAndTable();

        var changed = XlsxEditor.SetCells(bytes, sheet: null, [(Address("D1"), Json("\"заметка\""))]);

        var book = XlsxReader.Read(changed);
        Assert.Equal("Продажи", book.Find(null)!.Name);
        Assert.Contains(book.Find("Продажи")!.Cells, cell => cell.Value == "заметка");
        Assert.DoesNotContain(book.Find("Настройки")!.Cells, cell => cell.Value == "заметка");
    }

    [Fact]
    public void EmptyValue_ClearsTheCell()
    {
        var changed = XlsxEditor.SetCells(XlsxWriter.Create([Prices]), null, [(Address("A2"), Json("\"\""))]);

        Assert.DoesNotContain(XlsxReader.Read(changed).Sheets[0].Cells, cell => cell.Address.ToString() == "A2");
    }

    // Writing into a shared formula group expands it into plain formulas; the calculation chain is removed.
    [Fact]
    public void SetCells_InSharedFormula_ExpandsTheGroup()
    {
        var changed = XlsxEditor.SetCells(TestDocuments.ExcelLikeWorkbook(), sheet: null, [(Address("B2"), Json("100"))]);

        using var document = SpreadsheetDocument.Open(new MemoryStream(changed), false);
        Assert.Null(document.WorkbookPart!.CalculationChainPart);
        var formulas = document.WorkbookPart.WorksheetParts.SelectMany(part => part.Worksheet!.Descendants<CellFormula>()).ToList();
        Assert.DoesNotContain(formulas, formula => formula.FormulaType is not null);
        var cells = XlsxReader.Read(changed).Sheets[0].Cells.ToDictionary(cell => cell.Address.ToString());
        Assert.Equal(("100", null), (cells["B2"].Value, cells["B2"].Formula));
        Assert.Equal("A3*2", cells["B3"].Formula);
        Assert.Equal("A4*2", cells["B4"].Formula);
    }

    [Fact]
    public void SetCells_RowsKeepTheirOrder()
    {
        var bytes = XlsxWriter.Create([new SheetInput("Лист", [Row("a"), Row("b")])]);

        var changed = XlsxEditor.SetCells(bytes, null, [(Address("C10"), Json("1")), (Address("B1"), Json("2")), (Address("A5"), Json("3"))]);

        using var document = SpreadsheetDocument.Open(new MemoryStream(changed), false);
        var rows = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Row>().Select(row => row.RowIndex!.Value).ToList();
        Assert.Equal([1U, 2U, 5U, 10U], rows);
        var first = document.WorkbookPart.WorksheetParts.Single().Worksheet!.Descendants<Row>().First().Elements<Cell>().Select(cell => cell.CellReference!.Value).ToList();
        Assert.Equal(["A1", "B1"], first);
    }

    [Fact]
    public void AddSheet_AndRename_UpdateReferencesInFormulas()
    {
        var bytes = XlsxEditor.AddSheet(TestDocuments.ExcelLikeWorkbook(), new SheetInput("Новый", [Row("x")]));

        var renamed = XlsxEditor.RenameSheet(bytes, "Данные", "Продажи 2026");

        var book = XlsxReader.Read(renamed);
        Assert.Equal(["Продажи 2026", "Итоги", "Новый"], book.Sheets.Select(sheet => sheet.Name));
        Assert.Equal("SUM('Продажи 2026'!B2:B4)", book.Find("Итоги")!.Cells.Single().Formula);
    }

    // As in Excel, conditional formatting, data validation and hyperlinks on other sheets follow the renamed sheet.
    [Fact]
    public void Rename_UpdatesConditionalFormattingValidationAndLinks()
    {
        var bytes = XlsxWriter.Create([new SheetInput("Данные", [Row("x")]), new SheetInput("Итоги", [Row("y")])]);
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes);
            using (var document = SpreadsheetDocument.Open(stream, true))
            {
                var totals = document.WorkbookPart!.WorksheetParts.Last().Worksheet!;
                totals.Append(
                    new ConditionalFormatting(new ConditionalFormattingRule(new Formula("Данные!A1>0")) { Type = ConditionalFormatValues.Expression, Priority = 1 }) { SequenceOfReferences = new() { InnerText = "A1" } },
                    new DataValidations(new DataValidation(new Formula1("Данные!$A$1:$A$3")) { Type = DataValidationValues.List, SequenceOfReferences = new() { InnerText = "B1" } }) { Count = 1 },
                    new Hyperlinks(new Hyperlink { Reference = "C1", Location = "'Данные'!A1" }));
            }

            bytes = stream.ToArray();
        }

        var renamed = XlsxEditor.RenameSheet(bytes, "Данные", "Исходные данные");

        using var result = SpreadsheetDocument.Open(new MemoryStream(renamed), false);
        var worksheet = result.WorkbookPart!.WorksheetParts.Last().Worksheet!;
        Assert.Equal("'Исходные данные'!A1>0", worksheet.Descendants<Formula>().Single().Text);
        Assert.Equal("'Исходные данные'!$A$1:$A$3", worksheet.Descendants<Formula1>().Single().Text);
        Assert.Equal("'Исходные данные'!A1", worksheet.Descendants<Hyperlink>().Single().Location!.Value);
    }

    [Theory]
    [InlineData("Итоги")]
    [InlineData("a/b")]
    [InlineData("")]
    public void AddSheet_WithTakenOrInvalidName_IsRefused(string name) =>
        Assert.Throws<AgentToolException>(() => XlsxEditor.AddSheet(TestDocuments.ExcelLikeWorkbook(), new SheetInput(name, [])));

    [Fact]
    public void SheetText_ShowsAddressesAndFormulas()
    {
        var sheet = XlsxReader.Read(XlsxWriter.Create([Prices])).Sheets[0];

        var text = SheetText.Table(sheet, range: null);

        Assert.Contains("|   | A | B | C | D |", text, StringComparison.Ordinal);
        Assert.Contains("| 2 | Яблоки | 3 | 1.5 | =B2*C2 |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SheetText_LimitsRowsAndSaysHowToContinue()
    {
        var rows = Enumerable.Range(1, SheetText.MaxRows + 20).Select(number => Row(number)).ToArray();
        var sheet = XlsxReader.Read(XlsxWriter.Create([new SheetInput("Много", rows) { Header = false }])).Sheets[0];

        var text = SheetText.Table(sheet, range: null);

        Assert.DoesNotContain($"| {SheetText.MaxRows + 1} |", text, StringComparison.Ordinal);
        Assert.Contains($"A{SheetText.MaxRows + 1}:A{SheetText.MaxRows + 20}", text, StringComparison.Ordinal);
    }

    private static CellAddress Address(string text) => CellAddress.TryParse(text, out var address) ? address : throw new ArgumentException(text);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement[] Row(params object[] values) => [.. values.Select(value => JsonSerializer.SerializeToElement(value))];
}
