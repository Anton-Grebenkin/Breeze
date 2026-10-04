using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>Test documents hand-built with the Open XML SDK: shapes the module itself never writes.</summary>
internal static class TestDocuments
{
    /// <summary>Word package whose document part is broken XML: parsing fails on read, not on open.</summary>
    public static byte[] WordWithBrokenXml()
    {
        using var stream = new MemoryStream();
        stream.Write(BareWord("Текст"));
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("word/document.xml")!;
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open());
            writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p>");
        }

        return stream.ToArray();
    }

    /// <summary>Single-paragraph Word document without styles or numbering, as written by a third-party app.</summary>
    public static byte[] BareWord(string text) => Word(new W.Paragraph(new W.Run(new W.Text(text))));

    /// <summary>A PAGE field with result 7, text deleted and moved under tracked changes, and a hyperlink.</summary>
    public static byte[] WordWithFieldsAndRevisions()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var link = main.AddHyperlinkRelationship(new Uri("https://example.com/"), isExternal: true);
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(
                    new W.Run(new W.Text("Страница ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
                    new W.Run(new W.FieldCode(" PAGE ")),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
                    new W.Run(new W.Text("7")),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }),
                    new W.DeletedRun(new W.Run(new W.DeletedText("удалено"))),
                    new W.MoveFromRun(new W.Run(new W.Text("перенесено"))) { Id = "1", Author = "Тест" },
                    new W.Run(new W.Text(" из отчёта") { Space = SpaceProcessingModeValues.Preserve })),
                new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("ссылка"))) { Id = link.Id })));
        }

        return stream.ToArray();
    }

    private static byte[] Word(params W.Paragraph[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            document.AddMainDocumentPart().Document = new W.Document(new W.Body(paragraphs));
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Excel-like workbook: shared strings, a date with format 14, a boolean, an error and a shared formula B2:B4 = A*2
    /// with cached values; the totals sheet references the data sheet.
    /// </summary>
    public static byte[] ExcelLikeWorkbook()
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.AddNewPart<SharedStringTablePart>().SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("Число")),
                new SharedStringItem(new Run(new Text("Дво")), new Run(new Text("йное"))));
            workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = new Stylesheet(
                new Fonts(new Font()),
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None })),
                new Borders(new Border()),
                new CellFormats(new CellFormat { NumberFormatId = 0 }, new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }));
            var data = Sheet(workbook,
                new Row(Cell("A1", "0", CellValues.SharedString), Cell("B1", "1", CellValues.SharedString), Cell("C1", "45568", styleIndex: 1)) { RowIndex = 1 },
                new Row(Cell("A2", "1"), Formula("B2", "A2*2", "2", reference: "B2:B4"), Cell("C2", "1", CellValues.Boolean)) { RowIndex = 2 },
                new Row(Cell("A3", "2"), Formula("B3", null, "4"), Cell("C3", "#DIV/0!", CellValues.Error)) { RowIndex = 3 },
                new Row(Cell("A4", "3"), Formula("B4", null, "6")) { RowIndex = 4 });
            var totals = Sheet(workbook, new Row(new Cell(new CellFormula("SUM(Данные!B2:B4)"), new CellValue("12")) { CellReference = "A1" }) { RowIndex = 1 });
            workbook.Workbook = new Workbook(new Sheets(
                new Sheet { Name = "Данные", SheetId = 1, Id = workbook.GetIdOfPart(data) },
                new Sheet { Name = "Итоги", SheetId = 2, Id = workbook.GetIdOfPart(totals) }));
            workbook.AddNewPart<CalculationChainPart>().CalculationChain = new CalculationChain(new CalculationCell { CellReference = "B2", SheetId = 1 });
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Workbook whose first (settings) sheet is hidden; the second (sales) sheet holds table Table1 in A1:B3 with name
    /// and amount columns.
    /// </summary>
    public static byte[] WorkbookWithHiddenSheetAndTable()
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            var settings = Sheet(workbook, new Row(Cell("A1", "1")) { RowIndex = 1 });
            var sales = Sheet(workbook,
                new Row(Inline("A1", "Имя"), Inline("B1", "Сумма")) { RowIndex = 1 },
                new Row(Inline("A2", "Иван"), Cell("B2", "10")) { RowIndex = 2 },
                new Row(Inline("A3", "Пётр"), Cell("B3", "20")) { RowIndex = 3 });
            var table = sales.AddNewPart<TableDefinitionPart>();
            table.Table = new Table(
                new AutoFilter { Reference = "A1:B3" },
                new TableColumns(new TableColumn { Id = 1U, Name = "Имя" }, new TableColumn { Id = 2U, Name = "Сумма" }) { Count = 2U },
                new TableStyleInfo { Name = "TableStyleMedium2", ShowRowStripes = true })
            {
                Id = 1U, Name = "Table1", DisplayName = "Table1", Reference = "A1:B3",
            };
            sales.Worksheet!.Append(new TableParts(new TablePart { Id = sales.GetIdOfPart(table) }) { Count = 1U });
            workbook.Workbook = new Workbook(new Sheets(
                new Sheet { Name = "Настройки", SheetId = 1, Id = workbook.GetIdOfPart(settings), State = SheetStateValues.Hidden },
                new Sheet { Name = "Продажи", SheetId = 2, Id = workbook.GetIdOfPart(sales) }));
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Numbers distinguished only by format: A1 is 1.5 with "[h]:mm:ss", A2 is 1.5 with built-in format 46
    /// ("[h]:mm:ss"), A3 is half an hour with "mm:ss", A4 is 45568.5 with "dd.mm.yyyy hh:mm", A5 is 2.5 with color
    /// "[Magenta]0.00", A6 is 1 with date format 14.
    /// </summary>
    /// <param name="date1904">Use the 1904 date system, as in Excel for Mac workbooks.</param>
    public static byte[] WorkbookWithNumberFormats(bool date1904)
    {
        string[] values = ["1.5", "1.5", "0.0208333333333333", "45568.5", "2.5", "1"];
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164U, FormatCode = "[h]:mm:ss" },
                    new NumberingFormat { NumberFormatId = 165U, FormatCode = "mm:ss" },
                    new NumberingFormat { NumberFormatId = 166U, FormatCode = "dd.mm.yyyy hh:mm" },
                    new NumberingFormat { NumberFormatId = 167U, FormatCode = "[Magenta]0.00" }) { Count = 4U },
                new Fonts(new Font()),
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None })),
                new Borders(new Border()),
                new CellFormats(new uint[] { 0, 164, 46, 165, 166, 167, 14 }.Select(id => new CellFormat { NumberFormatId = id, ApplyNumberFormat = true })));
            var rows = values.Select((value, index) => new Row(Cell($"A{index + 1}", value, styleIndex: (uint)index + 1)) { RowIndex = (uint)index + 1 });
            var sheet = Sheet(workbook, [.. rows]);
            workbook.Workbook = new Workbook(
                new WorkbookProperties { Date1904 = date1904 },
                new Sheets(new Sheet { Name = "Время", SheetId = 1, Id = workbook.GetIdOfPart(sheet) }));
        }

        return stream.ToArray();
    }

    private static Cell Inline(string reference, string text) =>
        new(new InlineString(new Text(text))) { CellReference = reference, DataType = CellValues.InlineString };

    private static WorksheetPart Sheet(WorkbookPart workbook, params Row[] rows)
    {
        var part = workbook.AddNewPart<WorksheetPart>();
        part.Worksheet = new Worksheet(new SheetDimension { Reference = "A1:C4" }, new SheetData(rows));
        return part;
    }

    private static Cell Cell(string reference, string value, CellValues? type = null, uint? styleIndex = null)
    {
        var cell = new Cell(new CellValue(value)) { CellReference = reference };
        if (type is { } dataType)
        {
            cell.DataType = dataType;
        }

        if (styleIndex is { } style)
        {
            cell.StyleIndex = style;
        }

        return cell;
    }

    // Shared formula group 0: the first cell holds the text and range, the others only the group index.
    private static Cell Formula(string address, string? text, string value, string? reference = null)
    {
        var formula = new CellFormula(text ?? string.Empty) { FormulaType = CellFormulaValues.Shared, SharedIndex = 0U };
        if (reference is not null)
        {
            formula.Reference = reference;
        }

        return new Cell(formula, new CellValue(value)) { CellReference = address };
    }
}
