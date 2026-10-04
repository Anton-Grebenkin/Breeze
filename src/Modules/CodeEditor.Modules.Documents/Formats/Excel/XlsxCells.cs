using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Documents.Resources;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Writes a model value to an Excel cell by the same rules as typing into Excel: "12.5" is a number, "TRUE" a boolean,
/// "=SUM(…)" a formula (Excel computes it on open), "'00123" literal text, empty an empty cell. Numbers have no leading
/// zeros: "00123" stays text, like a code or a postal index. JSON numbers and booleans are taken as is. Text is stored
/// inline, so the edit leaves the shared string table alone. The cell's formatting is kept.
/// </summary>
internal static partial class XlsxCells
{
    private const int FormulaWidth = 10;

    public static void Write(Cell cell, JsonElement value)
    {
        Clear(cell);
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetDouble(out var number) && double.IsFinite(number):
                WriteNumber(cell, number);
                break;
            case JsonValueKind.True or JsonValueKind.False:
                WriteBoolean(cell, value.GetBoolean());
                break;
            case JsonValueKind.String:
                WriteTyped(cell, value.GetString()!);
                break;
            case JsonValueKind.Null or JsonValueKind.Undefined:
                break;
            default:
                WriteText(cell, value.GetRawText());
                break;
        }
    }

    public static void Clear(Cell cell)
    {
        cell.CellFormula = null;
        cell.CellValue = null;
        cell.InlineString = null;
        cell.DataType = null;
    }

    /// <summary>The value for the approval card as the model sent it; an empty one shows as "(empty)".</summary>
    public static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "TRUE",
        JsonValueKind.False => "FALSE",
        JsonValueKind.String when value.GetString() is { Length: > 0 } text => text,
        JsonValueKind.String or JsonValueKind.Null or JsonValueKind.Undefined => Strings.EmptyCell,
        _ => value.GetRawText(),
    };

    /// <summary>Value width in characters, for fitting the column width to content.</summary>
    public static int DisplayLength(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String when Formula(value.GetString()!) is not null => FormulaWidth,
        JsonValueKind.String => value.GetString()!.Split('\n').Max(line => line.Length),
        JsonValueKind.Null or JsonValueKind.Undefined => 0,
        _ => Describe(value).Length,
    };

    /// <summary>The formula without '=', or <c>null</c> if the text is not a formula.</summary>
    public static string? Formula(string text) => text.Length > 1 && text[0] == '=' ? text[1..] : null;

    private static void WriteTyped(Cell cell, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (Formula(text) is { } formula)
        {
            FormulaCheck.Validate(formula);
            cell.CellFormula = new CellFormula(formula);
        }
        else if (text[0] == '\'')
        {
            WriteText(cell, text[1..]);
        }
        else if (Number().IsMatch(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            WriteNumber(cell, number);
        }
        else if (bool.TryParse(text, out var flag))
        {
            WriteBoolean(cell, flag);
        }
        else
        {
            WriteText(cell, text);
        }
    }

    private static void WriteNumber(Cell cell, double number) => cell.CellValue = new CellValue(number.ToString("R", CultureInfo.InvariantCulture));

    private static void WriteBoolean(Cell cell, bool value)
    {
        cell.DataType = CellValues.Boolean;
        cell.CellValue = new CellValue(value ? "1" : "0");
    }

    private static void WriteText(Cell cell, string text)
    {
        cell.DataType = CellValues.InlineString;
        cell.InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$")]
    private static partial Regex Number();
}
