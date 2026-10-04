using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Viewers.Resources;

namespace CodeEditor.Modules.Viewers.Services;

/// <summary>File size for the tab info: "512 bytes", "1.5 KB", "45.2 MB", "123 MB"; base 1024, as in Explorer.</summary>
public static class ByteSizes
{
    private const double Unit = 1024;

    // Below a hundred, one decimal: "45.2 MB"; above it the decimal no longer matters: "123 MB".
    private const double WholeFrom = 100;

    public static string Format(long bytes)
    {
        if (bytes < Unit)
        {
            return Plural.Format(bytes, Strings.ByteForms);
        }

        string[] units = [Strings.SizeKilobytes, Strings.SizeMegabytes, Strings.SizeGigabytes, Strings.SizeTerabytes];
        var value = bytes / Unit;
        var unit = 0;
        while (value >= Unit && unit < units.Length - 1)
        {
            value /= Unit;
            unit++;
        }

        var number = value.ToString(value < WholeFrom ? "0.#" : "0", CultureInfo.CurrentCulture);
        return string.Format(CultureInfo.CurrentCulture, units[unit], number);
    }

    /// <summary>The rounded size plus the exact byte count: "2.4 MB (2,516,582 bytes)".</summary>
    public static string FormatExact(long bytes) =>
        bytes < Unit ? Format(bytes) : string.Format(CultureInfo.CurrentCulture, Strings.SizeExact, Format(bytes), Plural.Format(bytes, Strings.ByteForms));
}
