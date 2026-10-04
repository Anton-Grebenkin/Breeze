using System.Globalization;

namespace CodeEditor.Core.Text;

/// <summary>
/// Plural forms in the UI language. Forms come from resources as one "|"-separated string: three for Russian
/// (singular, few, many: 1, 2–4, 5–20, so 21 is singular and 11 is many), two for English ("file|files").
/// The rule is picked by the number of forms, so the code is language-agnostic.
/// </summary>
public static class Plural
{
    private const char Separator = '|';
    private const int RussianFormCount = 3;

    // Russian digit groups use a plain space ("20 000"): the ru-RU non-breaking space is wider in monospace text.
    private static readonly NumberFormatInfo SpaceGroups = new() { NumberGroupSeparator = " ", NumberGroupSizes = [3] };

    public static string Select(long count, string forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        var variants = forms.Split(Separator);
        return variants.Length switch
        {
            RussianFormCount => variants[RussianIndex(count)],
            > 1 => Math.Abs(count) == 1 ? variants[0] : variants[1],
            _ => variants[0],
        };
    }

    /// <summary>Number with its form, e.g. "3 files".</summary>
    public static string Format(long count, string forms) =>
        $"{count.ToString("#,0", Numbers())} {Select(count, forms)}";

    private static int RussianIndex(long count)
    {
        var lastTwo = Math.Abs(count) % 100;
        var last = lastTwo % 10;
        return lastTwo is >= 11 and <= 14 ? 2
            : last == 1 ? 0
            : last is >= 2 and <= 4 ? 1
            : 2;
    }

    private static NumberFormatInfo Numbers() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? SpaceGroups : CultureInfo.InvariantCulture.NumberFormat;
}
