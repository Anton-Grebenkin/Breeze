using System.Text.Json;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// A new Excel workbook from the model's sheets: values and formulas from A1, a bold frozen header row, column widths
/// fitted to the longest value. Excel computes formulas on open (<c>fullCalcOnLoad</c>).
/// </summary>
internal static class XlsxWriter
{
    private const int MinWidth = 6;
    private const int MaxWidth = 60;
    private const int WidthPadding = 2;

    public static byte[] Create(IReadOnlyList<SheetInput> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.AddNewPart<WorkbookStylesPart>().Stylesheet = XlsxStyles.Create();
            var list = new Sheets();
            workbook.Workbook = new Workbook(list, new CalculationProperties { FullCalculationOnLoad = true });
            for (var index = 0; index < sheets.Count; index++)
            {
                AddSheet(workbook, list, sheets[index], (uint)index + 1, XlsxStyles.BoldFormat, selected: index == 0);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Adds a sheet; <paramref name="boldFormat"/> is the bold format index of this workbook.</summary>
    public static void AddSheet(WorkbookPart workbook, Sheets list, SheetInput input, uint sheetId, uint boldFormat, bool selected)
    {
        var part = workbook.AddNewPart<WorksheetPart>();
        part.Worksheet = Worksheet(input, boldFormat, selected);
        list.Append(new Sheet { Name = input.Name, SheetId = sheetId, Id = workbook.GetIdOfPart(part) });
    }

    private static Worksheet Worksheet(SheetInput input, uint boldFormat, bool selected)
    {
        var rows = input.Rows ?? [];
        var worksheet = new Worksheet(View(input.Header && rows.Length > 1, selected));
        if (Widths(rows) is { ChildElements.Count: > 0 } columns)
        {
            worksheet.Append(columns);
        }

        var data = new SheetData();
        for (var row = 0; row < rows.Length; row++)
        {
            var element = new Row { RowIndex = (uint)row + 1 };
            for (var column = 0; column < (rows[row]?.Length ?? 0); column++)
            {
                var value = rows[row][column];
                if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || (value.ValueKind == JsonValueKind.String && value.GetString() is ""))
                {
                    continue;
                }

                var cell = new Cell { CellReference = new CellAddress(row + 1, column + 1).ToString() };
                XlsxCells.Write(cell, value);
                if (row == 0 && input.Header)
                {
                    cell.StyleIndex = boldFormat;
                }

                element.Append(cell);
            }

            data.Append(element);
        }

        worksheet.Append(data);
        return worksheet;
    }

    // Frozen first row keeps the header visible while scrolling a long table.
    private static SheetViews View(bool freezeHeader, bool selected)
    {
        var view = new SheetView { WorkbookViewId = 0U, TabSelected = selected };
        if (freezeHeader)
        {
            view.Append(
                new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen },
                new Selection { Pane = PaneValues.BottomLeft, ActiveCell = "A2", SequenceOfReferences = new ListValue<StringValue> { InnerText = "A2" } });
        }

        return new SheetViews(view);
    }

    private static Columns Widths(JsonElement[][] rows)
    {
        var widths = new List<int>();
        foreach (var row in rows)
        {
            for (var column = 0; column < (row?.Length ?? 0); column++)
            {
                if (column == widths.Count)
                {
                    widths.Add(0);
                }

                widths[column] = Math.Max(widths[column], XlsxCells.DisplayLength(row![column]));
            }
        }

        return new Columns(widths.Select((width, index) => new Column
        {
            Min = (uint)index + 1,
            Max = (uint)index + 1,
            Width = Math.Clamp(width + WidthPadding, MinWidth, MaxWidth),
            CustomWidth = true,
        }));
    }
}
