using System.Globalization;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>A rectangle of cells: "A1:D20", a single cell "B3", columns "B:D" or rows "3:10".</summary>
public readonly record struct CellRange(CellAddress Start, CellAddress End)
{
    public int RowCount => End.Row - Start.Row + 1;

    public int ColumnCount => End.Column - Start.Column + 1;

    public static bool TryParse(string? text, out CellRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            var parsed = CellAddress.TryParse(parts[0], out var single);
            range = new CellRange(single, single);
            return parsed;
        }

        return parts.Length == 2 && (TryCells(parts[0], parts[1], out range) || TryColumns(parts[0], parts[1], out range) || TryRows(parts[0], parts[1], out range));
    }

    public bool Contains(int row, int column) => row >= Start.Row && row <= End.Row && column >= Start.Column && column <= End.Column;

    public override string ToString() => Start == End ? Start.ToString() : $"{Start}:{End}";

    private static bool TryCells(string first, string last, out CellRange range)
    {
        range = default;
        if (!CellAddress.TryParse(first, out var start) || !CellAddress.TryParse(last, out var end))
        {
            return false;
        }

        range = Ordered(start.Row, start.Column, end.Row, end.Column);
        return true;
    }

    private static bool TryColumns(string first, string last, out CellRange range)
    {
        range = default;
        if (!IsLetters(first) || !IsLetters(last))
        {
            return false;
        }

        range = Ordered(1, CellAddress.ColumnNumber(first.TrimStart('$')), CellAddress.MaxRow, CellAddress.ColumnNumber(last.TrimStart('$')));
        return range.End.Column <= CellAddress.MaxColumn;
    }

    private static bool TryRows(string first, string last, out CellRange range)
    {
        range = default;
        if (!int.TryParse(first.TrimStart('$'), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(last.TrimStart('$'), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || start < 1 || end < 1)
        {
            return false;
        }

        range = Ordered(start, 1, end, CellAddress.MaxColumn);
        return range.End.Row <= CellAddress.MaxRow;
    }

    private static bool IsLetters(string text)
    {
        var letters = text.TrimStart('$');
        return letters.Length is > 0 and <= 3 && letters.All(char.IsAsciiLetter);
    }

    private static CellRange Ordered(int row1, int column1, int row2, int column2) =>
        new(new CellAddress(Math.Min(row1, row2), Math.Min(column1, column2)), new CellAddress(Math.Max(row1, row2), Math.Max(column1, column2)));
}
