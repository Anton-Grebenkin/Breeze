using System.Globalization;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>Short token count in the UI language: <c>850</c>, <c>24.3K</c>, <c>1.05M</c>.</summary>
public static class TokenText
{
    private const double Thousand = 1_000;
    private const double Million = 1_000_000;

    public static string Format(long tokens)
    {
        var culture = CultureInfo.CurrentUICulture;
        return tokens switch
        {
            < (long)Thousand => tokens.ToString(culture),
            < (long)Million => string.Format(culture, Strings.TokensThousands, (tokens / Thousand).ToString("0.#", culture)),
            _ => string.Format(culture, Strings.TokensMillions, (tokens / Million).ToString("0.##", culture)),
        };
    }

    /// <summary>A fraction as a whole percentage: <c>12%</c>.</summary>
    public static string Percent(double fraction) =>
        Math.Round(fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
