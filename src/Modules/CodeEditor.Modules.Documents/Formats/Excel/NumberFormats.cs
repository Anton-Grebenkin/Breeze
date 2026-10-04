using System.Text;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Excel number formats: date, time or duration. A date cell holds days since the workbook's date system epoch, and
/// only its format tells it apart: a built-in one (14–22, 45–47, East Asian 27–36 and 50–58) or a custom one with the
/// letters y, m, d, h, s outside quotes and brackets. A duration has hours, minutes or seconds in square brackets
/// ("[h]:mm"): they do not roll over into days.
/// </summary>
internal static partial class NumberFormats
{
    private const uint ElapsedHours = 46;
    private static readonly HashSet<uint> BuiltInDates = [14, 15, 16, 17, 22, .. Range(27, 36), .. Range(50, 58)];
    private static readonly HashSet<uint> BuiltInTimes = [18, 19, 20, 21, 45, 47];

    /// <summary>
    /// What the format shows. A custom code wins over the built-in id: workbooks from localized Excel override
    /// built-in formats too.
    /// </summary>
    public static NumberKind Classify(uint id, string? code)
    {
        if (code is not null)
        {
            return Classify(code);
        }

        if (id == ElapsedHours)
        {
            return NumberKind.Duration;
        }

        return BuiltInTimes.Contains(id) ? NumberKind.Time : BuiltInDates.Contains(id) ? NumberKind.Date : NumberKind.Number;
    }

    private static NumberKind Classify(string code)
    {
        if (Elapsed().IsMatch(code))
        {
            return NumberKind.Duration;
        }

        var letters = Letters(code);
        if (letters.Contains("general", StringComparison.Ordinal) || letters.AsSpan().IndexOfAny("ymdhs") < 0)
        {
            return NumberKind.Number;
        }

        return letters.AsSpan().IndexOfAny("yd") < 0 && letters.AsSpan().IndexOfAny("hs") >= 0 ? NumberKind.Time : NumberKind.Date;
    }

    // Format letters without quoted text, [Red] or [$-419], and escaped characters "\x", "_x" and "*x".
    private static string Letters(string code)
    {
        var result = new StringBuilder(code.Length);
        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];
            switch (character)
            {
                case '"':
                    index = SkipTo(code, index, '"');
                    break;
                case '[':
                    index = SkipTo(code, index, ']');
                    break;
                case '\\' or '_' or '*':
                    index++;
                    break;
                default:
                    result.Append(char.ToLowerInvariant(character));
                    break;
            }
        }

        return result.ToString();
    }

    private static int SkipTo(string code, int index, char closing)
    {
        var end = code.IndexOf(closing, index + 1);
        return end < 0 ? code.Length : end;
    }

    private static IEnumerable<uint> Range(uint first, uint last)
    {
        for (var id = first; id <= last; id++)
        {
            yield return id;
        }
    }

    // Elapsed time "[h]", "[mm]", "[ss]", but not the color "[Magenta]" or the locale "[$-419]".
    [GeneratedRegex(@"\[(?:h+|m+|s+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Elapsed();
}
