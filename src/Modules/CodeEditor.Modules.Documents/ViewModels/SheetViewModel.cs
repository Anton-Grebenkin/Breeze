using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// A sheet in the grid viewer: columns from A to the last one with data, rows from the first to the last, numbered as in
/// Excel. Built in the background while reading the file. A cell value is shown as Excel would: a number in the current
/// culture, a date, and a formula without a cached value as its text.
/// </summary>
public sealed class SheetViewModel
{
    /// <param name="maxRows">Row limit used when reading: a sheet this long is most likely truncated.</param>
    public SheetViewModel(SpreadsheetSheet sheet, CultureInfo culture, int maxColumns, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        Name = sheet.Name;
        IsHidden = sheet.IsHidden;
        var columnCount = Math.Min(sheet.ColumnCount, maxColumns);
        Columns = [.. Enumerable.Range(1, columnCount).Select(CellAddress.ColumnName)];
        IsTruncated = sheet.ColumnCount > maxColumns || sheet.RowCount >= maxRows;
        Rows = BuildRows(sheet, culture, columnCount);
        Summary = sheet.RowCount == 0
            ? Strings.EmptySheet
            : Plural.Format(sheet.RowCount, Strings.RowForms) + ", " + Plural.Format(sheet.ColumnCount, Strings.ColumnForms);
    }

    public string Name { get; }

    public bool IsHidden { get; }

    /// <summary>Sheet tab caption: a hidden sheet is marked.</summary>
    public string Title => IsHidden ? string.Format(CultureInfo.CurrentCulture, Strings.HiddenSheetTitle, Name) : Name;

    /// <summary>Column letters: A, B, …</summary>
    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<SheetRowViewModel> Rows { get; }

    /// <summary>There are more rows or columns than shown.</summary>
    public bool IsTruncated { get; }

    public string Summary { get; }

    private static SheetRowViewModel[] BuildRows(SpreadsheetSheet sheet, CultureInfo culture, int columnCount)
    {
        var cells = new Dictionary<int, string>[sheet.RowCount];
        foreach (var cell in sheet.Cells)
        {
            if (cell.Column <= columnCount)
            {
                (cells[cell.Row - 1] ??= [])[cell.Column] = Display(cell, culture);
            }
        }

        var empty = new Dictionary<int, string>();
        var rows = new SheetRowViewModel[cells.Length];
        for (var index = 0; index < cells.Length; index++)
        {
            rows[index] = new SheetRowViewModel(index + 1, cells[index] ?? empty);
        }

        return rows;
    }

    private static string Display(SpreadsheetCell cell, CultureInfo culture) =>
        cell.Value.Length == 0 && cell.Formula is { } formula ? "=" + formula : cell.DisplayValue(culture);
}
