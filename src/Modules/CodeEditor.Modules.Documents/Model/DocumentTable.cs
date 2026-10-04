using System.Collections.Immutable;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>Document table: rows of cells, the first row is the header. Merged cells become empty neighbors.</summary>
public sealed record DocumentTable(ImmutableArray<ImmutableArray<ImmutableArray<TextRun>>> Rows)
{
    public int ColumnCount => Rows.IsEmpty ? 0 : Rows.Max(row => row.Length);

    /// <summary>Cell text without styles; empty if the row has no such cell.</summary>
    public string CellText(int row, int column) =>
        column < Rows[row].Length ? string.Concat(Rows[row][column].Select(run => run.Text)) : string.Empty;
}
