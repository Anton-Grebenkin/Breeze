using System.Collections.Immutable;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Excel workbook (.xlsx) to sheets of non-empty cells: value, formula (shared formulas with shifted references), kind.
/// Sheet rows are streamed (<see cref="OpenXmlReader"/>), so the sheet XML is never built in memory. Formula values are
/// the ones Excel cached; formulas written without Excel are computed when Excel opens the file. O(n) in cells.
/// </summary>
internal static class XlsxReader
{
    /// <exception cref="InvalidDataException">The file is not an Excel workbook.</exception>
    /// <param name="maxRows">The last sheet row to read, inclusive; limits the viewer on huge sheets.</param>
    public static Spreadsheet Read(byte[] bytes, int maxRows = CellAddress.MaxRow)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = OpenXmlFiles.OpenExcel(stream, editable: false);
        var workbook = document.WorkbookPart ?? throw OpenXmlFiles.NotDocument();
        var values = XlsxValues.Load(workbook);
        var sheets = ImmutableArray.CreateBuilder<SpreadsheetSheet>();
        foreach (var sheet in workbook.Workbook?.Sheets?.Elements<Sheet>() ?? [])
        {
            var cells = sheet.Id?.Value is { } id && workbook.TryGetPartById(id, out var part) && part is WorksheetPart worksheet
                ? ReadCells(worksheet, values, maxRows)
                : [];
            var state = sheet.State?.Value;
            var hidden = state is { } value && (value == SheetStateValues.Hidden || value == SheetStateValues.VeryHidden);
            sheets.Add(new SpreadsheetSheet(sheet.Name?.Value ?? string.Empty, cells) { IsHidden = hidden });
        }

        return new Spreadsheet(sheets.ToImmutable());
    }

    private static ImmutableArray<SpreadsheetCell> ReadCells(WorksheetPart worksheet, XlsxValues values, int maxRows)
    {
        var cells = ImmutableArray.CreateBuilder<SpreadsheetCell>();
        var shared = new Dictionary<uint, (CellAddress Origin, string Formula)>();
        var previousRow = 0;
        using var reader = OpenXmlReader.Create(worksheet);
        reader.Read();
        while (!reader.EOF)
        {
            // LoadCurrentElement already advances to the next element: calling Read after it would skip a row.
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement)
            {
                reader.Read();
                continue;
            }

            var row = (Row)reader.LoadCurrentElement()!;
            var number = row.RowIndex?.Value is { } index ? (int)index : previousRow + 1;
            if (number > maxRows)
            {
                break;
            }

            previousRow = number;
            AddRow(cells, row, number, values, shared);
        }

        return cells.ToImmutable();
    }

    private static void AddRow(ImmutableArray<SpreadsheetCell>.Builder cells, Row row, int number, XlsxValues values, Dictionary<uint, (CellAddress Origin, string Formula)> shared)
    {
        var previousColumn = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            var address = cell.CellReference?.Value is { } reference && CellAddress.TryParse(reference, out var parsed)
                ? parsed
                : new CellAddress(number, previousColumn + 1);
            previousColumn = address.Column;
            if (values.Read(cell, address, Formula(cell.CellFormula, address, shared)) is { } value)
            {
                cells.Add(value);
            }
        }
    }

    // A shared formula's text is on the group's first cell; the others get it with shifted references.
    private static string? Formula(CellFormula? formula, CellAddress address, Dictionary<uint, (CellAddress Origin, string Formula)> shared)
    {
        if (formula is null)
        {
            return null;
        }

        var text = formula.Text;
        if (formula.FormulaType?.Value != CellFormulaValues.Shared || formula.SharedIndex?.Value is not { } group)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        if (!string.IsNullOrEmpty(text))
        {
            shared[group] = (address, text);
            return text;
        }

        return shared.TryGetValue(group, out var origin)
            ? FormulaReferences.Shift(origin.Formula, address.Row - origin.Origin.Row, address.Column - origin.Origin.Column)
            : null;
    }
}
