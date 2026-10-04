using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// A1 references in formulas. Excel stores a formula filled down only once (a shared formula); other cells get its text
/// by shifting relative references to cells, columns "B:B" and rows "3:3". Renaming a sheet rewrites references to it.
/// Quoted strings and anything in square brackets (table column references, external workbooks) stay untouched; a
/// function name like LOG10( is not a reference. O(n) in the formula length.
/// </summary>
internal static partial class FormulaReferences
{
    private const string BrokenReference = "#REF!";

    /// <summary>Shifts relative references by <paramref name="rows"/> rows and <paramref name="columns"/>.</summary>
    public static string Shift(string formula, int rows, int columns)
    {
        var result = new StringBuilder(formula.Length);
        foreach (var (text, kind) in FormulaSegments.Split(formula))
        {
            result.Append(kind == FormulaSegmentKind.Plain ? Reference().Replace(text, match => Shifted(match, rows, columns)) : text);
        }

        return result.ToString();
    }

    /// <summary>
    /// Points references to <paramref name="oldName"/> at <paramref name="newName"/>: "Data!A1", "'Old data'!A1".
    /// </summary>
    public static string RenameSheet(string formula, string oldName, string newName)
    {
        var result = new StringBuilder(formula.Length);
        var position = 0;
        var previous = FormulaSegmentKind.Plain;
        foreach (var (text, kind) in FormulaSegments.Split(formula))
        {
            position += text.Length;
            var isReference = position < formula.Length && formula[position] == '!';
            result.Append(kind switch
            {
                FormulaSegmentKind.SheetName when isReference && string.Equals(Unquote(text), oldName, StringComparison.OrdinalIgnoreCase) => Quote(newName),
                FormulaSegmentKind.Plain => SheetPrefix().Replace(text, match =>
                    // "[1]Data!A1" is a sheet of another workbook: not ours even if the name matches.
                    string.Equals(match.Groups["name"].Value, oldName, StringComparison.OrdinalIgnoreCase) && !(match.Index == 0 && previous == FormulaSegmentKind.Bracket)
                        ? Quote(newName) + "!"
                        : match.Value),
                _ => text,
            });
            previous = kind;
        }

        return result.ToString();
    }

    /// <summary>
    /// Sheet name for a formula: quoted unless it is only letters, digits and '_' and does not look like an address.
    /// </summary>
    public static string Quote(string sheetName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sheetName);
        var plain = sheetName.All(character => char.IsLetterOrDigit(character) || character == '_')
            && !char.IsDigit(sheetName[0]) && !CellAddress.TryParse(sheetName, out _);
        return plain ? sheetName : "'" + sheetName.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static string Unquote(string quoted) => quoted.Length >= 2 ? quoted[1..^1].Replace("''", "'", StringComparison.Ordinal) : quoted;

    private static string Shifted(Match match, int rows, int columns)
    {
        if (match.Groups["columns"].Success)
        {
            return Column(match.Groups["fc1"].Value, match.Groups["c1"].Value, columns) is { } first && Column(match.Groups["fc2"].Value, match.Groups["c2"].Value, columns) is { } last
                ? first + ":" + last
                : BrokenReference;
        }

        if (match.Groups["rows"].Success)
        {
            return Row(match.Groups["fr1"].Value, match.Groups["r1"].Value, rows) is { } first && Row(match.Groups["fr2"].Value, match.Groups["r2"].Value, rows) is { } last
                ? first + ":" + last
                : BrokenReference;
        }

        return Column(match.Groups["fixedColumn"].Value, match.Groups["column"].Value, columns) is { } column && Row(match.Groups["fixedRow"].Value, match.Groups["row"].Value, rows) is { } row
            ? column + row
            : BrokenReference;
    }

    // Relative parts shift, absolute ('$') ones do not; null when the result falls off the sheet.
    private static string? Column(string fixedMark, string letters, int offset)
    {
        var column = CellAddress.ColumnNumber(letters) + (fixedMark.Length > 0 ? 0 : offset);
        return column is < 1 or > CellAddress.MaxColumn ? null : fixedMark + CellAddress.ColumnName(column);
    }

    private static string? Row(string fixedMark, string digits, int offset)
    {
        var row = int.Parse(digits, CultureInfo.InvariantCulture) + (fixedMark.Length > 0 ? 0 : offset);
        return row is < 1 or > CellAddress.MaxRow ? null : fixedMark + row.ToString(CultureInfo.InvariantCulture);
    }

    // Columns "B:B", rows "3:3" or cell "A1", not preceded or followed by a letter, digit or name character.
    [GeneratedRegex(@"(?<![\p{L}\p{N}_.$])(?:(?<columns>(?<fc1>\$?)(?<c1>[A-Za-z]{1,3}):(?<fc2>\$?)(?<c2>[A-Za-z]{1,3}))|(?<rows>(?<fr1>\$?)(?<r1>[0-9]{1,7}):(?<fr2>\$?)(?<r2>[0-9]{1,7}))|(?<fixedColumn>\$?)(?<column>[A-Za-z]{1,3})(?<fixedRow>\$?)(?<row>[0-9]{1,7}))(?![\p{L}\p{N}_(!.])")]
    private static partial Regex Reference();

    [GeneratedRegex(@"(?<![\p{L}\p{N}_.])(?<name>[\p{L}_][\p{L}\p{N}_.]*)!")]
    private static partial Regex SheetPrefix();
}
