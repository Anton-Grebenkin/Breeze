using System.Globalization;
using System.Text;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// A sheet for the model as a Markdown table with column letters and row numbers, which the model uses to name cells
/// to edit. A formula shows as "=SUM(B2:B5) → 42" with the cached value after the arrow. Empty rows are skipped; rows
/// and columns are capped, and a trailing line tells how to read the rest.
/// </summary>
internal static class SheetText
{
    public const int MaxRows = 500;
    public const int MaxColumns = 100;

    /// <summary>The workbook's sheet list: "Data (A1:D120), Totals (A1:B5, hidden)".</summary>
    public static string SheetList(Spreadsheet book) => string.Join(", ", book.Sheets.Select(sheet =>
    {
        var details = new List<string> { sheet.UsedRange?.ToString() ?? Strings.EmptySheet };
        if (sheet.IsHidden)
        {
            details.Add(Strings.HiddenSheet);
        }

        return $"{sheet.Name} ({string.Join(", ", details)})";
    }));

    /// <param name="range"><c>null</c> for the sheet's used range from the start.</param>
    public static string Table(SpreadsheetSheet sheet, CellRange? range)
    {
        if (sheet.UsedRange is not { } used)
        {
            return Format(Strings.SheetIsEmpty, sheet.Name);
        }

        var requested = range ?? used;
        if (Intersect(requested, used) is not { } area)
        {
            return Format(Strings.RangeHasNoData, requested, sheet.Name, used);
        }

        var rows = sheet.Cells.Where(cell => area.Contains(cell.Row, cell.Column)).GroupBy(cell => cell.Row).OrderBy(group => group.Key).ToList();
        var shownRows = rows.Take(MaxRows).ToList();
        var lastColumn = Math.Min(area.End.Column, area.Start.Column + MaxColumns - 1);
        var output = new StringBuilder();
        output.Append(Format(Strings.SheetHeader, sheet.Name, area)).Append('\n');
        AppendHeader(output, area.Start.Column, lastColumn);
        foreach (var row in shownRows)
        {
            var cells = row.ToDictionary(cell => cell.Column);
            output.Append("| ").Append(row.Key.ToString(CultureInfo.InvariantCulture)).Append(" |");
            for (var column = area.Start.Column; column <= lastColumn; column++)
            {
                output.Append(' ').Append(cells.TryGetValue(column, out var cell) ? Escape(Show(cell)) : string.Empty).Append(" |");
            }

            output.Append('\n');
        }

        AppendLimits(output, rows, shownRows, area, lastColumn);
        return output.ToString().TrimEnd();
    }

    /// <summary>A cell for the model: the formula with its value, or just the value.</summary>
    public static string Show(SpreadsheetCell cell) => cell.Formula is { } formula
        ? cell.Value.Length > 0 ? $"={formula} → {cell.Value}" : "=" + formula
        : cell.Value;

    private static void AppendHeader(StringBuilder output, int first, int last)
    {
        output.Append("|   |");
        for (var column = first; column <= last; column++)
        {
            output.Append(' ').Append(CellAddress.ColumnName(column)).Append(" |");
        }

        output.Append("\n|---|").Append(string.Concat(Enumerable.Repeat("---|", last - first + 1))).Append('\n');
    }

    private static void AppendLimits(StringBuilder output, List<IGrouping<int, SpreadsheetCell>> rows, List<IGrouping<int, SpreadsheetCell>> shown, CellRange area, int lastColumn)
    {
        if (rows.Count > shown.Count)
        {
            var next = rows[shown.Count].Key;
            var more = new CellRange(new CellAddress(next, area.Start.Column), new CellAddress(rows[^1].Key, area.End.Column));
            output.Append(Format(Strings.MoreRows, shown.Count, rows.Count, more)).Append('\n');
        }

        if (lastColumn < area.End.Column)
        {
            output.Append(Format(Strings.MoreColumns, CellAddress.ColumnName(lastColumn + 1), CellAddress.ColumnName(area.End.Column))).Append('\n');
        }
    }

    // Narrows the requested range to the used one so "B:D" does not mean a million rows; null if they don't overlap.
    private static CellRange? Intersect(CellRange range, CellRange used)
    {
        var start = new CellAddress(Math.Max(range.Start.Row, used.Start.Row), Math.Max(range.Start.Column, used.Start.Column));
        var end = new CellAddress(Math.Min(range.End.Row, used.End.Row), Math.Min(range.End.Column, used.End.Column));
        return start.Row <= end.Row && start.Column <= end.Column ? new CellRange(start, end) : null;
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r\n", "<br>", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
