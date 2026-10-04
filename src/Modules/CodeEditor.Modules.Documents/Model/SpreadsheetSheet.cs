using System.Collections.Immutable;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>
/// A sheet: non-empty cells by row, then by column. The used range is computed once, on creation: the sheet list, the
/// model's table and the viewer all ask for it.
/// </summary>
public sealed class SpreadsheetSheet
{
    public SpreadsheetSheet(string name, ImmutableArray<SpreadsheetCell> cells)
    {
        Name = name;
        Cells = cells;
        UsedRange = Range(cells);
    }

    public string Name { get; }

    public ImmutableArray<SpreadsheetCell> Cells { get; }

    public bool IsHidden { get; init; }

    /// <summary>Used range such as "A1:D20"; <c>null</c> for an empty sheet.</summary>
    public CellRange? UsedRange { get; }

    /// <summary>Last row with data; 0 for an empty sheet.</summary>
    public int RowCount => UsedRange?.End.Row ?? 0;

    /// <summary>Last column with data; 0 for an empty sheet.</summary>
    public int ColumnCount => UsedRange?.End.Column ?? 0;

    // One pass over the cells: O(n).
    private static CellRange? Range(ImmutableArray<SpreadsheetCell> cells)
    {
        if (cells.IsEmpty)
        {
            return null;
        }

        var (top, left, bottom, right) = (int.MaxValue, int.MaxValue, 0, 0);
        foreach (var cell in cells)
        {
            top = Math.Min(top, cell.Row);
            left = Math.Min(left, cell.Column);
            bottom = Math.Max(bottom, cell.Row);
            right = Math.Max(right, cell.Column);
        }

        return new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
    }
}
