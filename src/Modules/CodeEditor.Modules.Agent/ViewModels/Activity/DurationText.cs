using System.Globalization;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>Formats a duration for the feed: "6 s", "1 min 5 s", "12 min"; under a second shows as "1 s".</summary>
public static class DurationText
{
    private const int SecondsPerMinute = 60;
    private const int MinutesWithoutSeconds = 10;

    public static string Format(TimeSpan duration)
    {
        var seconds = Math.Max(1, (int)Math.Round(duration.TotalSeconds));
        if (seconds < SecondsPerMinute)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.DurationSeconds, seconds);
        }

        var (minutes, rest) = Math.DivRem(seconds, SecondsPerMinute);
        return minutes >= MinutesWithoutSeconds || rest == 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.DurationMinutes, minutes)
            : string.Format(CultureInfo.CurrentCulture, Strings.DurationMinutesSeconds, minutes, rest);
    }
}
