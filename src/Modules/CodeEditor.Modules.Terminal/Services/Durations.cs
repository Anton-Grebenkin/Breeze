using System.Globalization;

namespace CodeEditor.Modules.Terminal.Services;

/// <summary>Seconds for build, test and command summaries in the UI culture ("2.5" or "2,5").</summary>
internal static class Durations
{
    public static string Seconds(TimeSpan elapsed) => elapsed.TotalSeconds.ToString("0.#", CultureInfo.CurrentUICulture);
}
