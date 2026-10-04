using System.Globalization;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>A1-style cell address: column letters, then the row number; the absolute-address "$" is allowed.</summary>
/// <param name="Row">1-based row.</param>
/// <param name="Column">1-based column: A is 1, Z is 26, AA is 27.</param>
public readonly record struct CellAddress(int Row, int Column)
{
    public const int MaxRow = 1_048_576;
    public const int MaxColumn = 16_384;

    private const int Letters = 26;
    private const int MaxColumnLetters = 3;

    public static bool TryParse(ReadOnlySpan<char> text, out CellAddress address)
    {
        address = default;
        var span = text.Trim();
        var position = span.StartsWith("$") ? 1 : 0;
        var lettersStart = position;
        while (position < span.Length && char.IsAsciiLetter(span[position]))
        {
            position++;
        }

        var letters = span[lettersStart..position];
        if (position < span.Length && span[position] == '$')
        {
            position++;
        }

        if (letters.IsEmpty || letters.Length > MaxColumnLetters
            || !int.TryParse(span[position..], NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row is < 1 or > MaxRow)
        {
            return false;
        }

        var column = ColumnNumber(letters);
        if (column > MaxColumn)
        {
            return false;
        }

        address = new CellAddress(row, column);
        return true;
    }

    /// <summary>Column number from letters: "A" is 1, "AB" is 28. The caller validates the letters.</summary>
    public static int ColumnNumber(ReadOnlySpan<char> letters)
    {
        var number = 0;
        foreach (var letter in letters)
        {
            number = number * Letters + (char.ToUpperInvariant(letter) - 'A' + 1);
        }

        return number;
    }

    /// <summary>Column letters: 1 is "A", 28 is "AB".</summary>
    public static string ColumnName(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(column);
        Span<char> buffer = stackalloc char[MaxColumnLetters + 1];
        var start = buffer.Length;
        for (var rest = column; rest > 0; rest = (rest - 1) / Letters)
        {
            buffer[--start] = (char)('A' + (rest - 1) % Letters);
        }

        return new string(buffer[start..]);
    }

    public override string ToString() => ColumnName(Column) + Row.ToString(CultureInfo.InvariantCulture);
}
