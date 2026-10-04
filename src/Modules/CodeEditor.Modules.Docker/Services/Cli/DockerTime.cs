using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Docker.Resources;

namespace CodeEditor.Modules.Docker.Services.Cli;

/// <summary>
/// Time in docker output. docker writes durations in English ("45 hours", "About an hour", "2 weeks") and creation time
/// as "2026-07-14 08:38:19 +0700 +07"; the panel shows them in the UI language. Rounding steps match docker's
/// (go-units HumanDuration), so the parsed and shown numbers agree.
/// </summary>
public static partial class DockerTime
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const int OffsetLength = 5;
    private const int HoursPerDay = 24;
    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    /// <summary>Parses "45 hours", "About a minute", "Less than a second"; <c>null</c> when unrecognized.</summary>
    public static TimeSpan? ParseDuration(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var match = Duration().Match(text.Trim());
        if (!match.Success)
        {
            return null;
        }

        var count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].ValueSpan, CultureInfo.InvariantCulture) : 1;
        return match.Groups["unit"].Value switch
        {
            "second" => TimeSpan.FromSeconds(count),
            "minute" => TimeSpan.FromMinutes(count),
            "hour" => TimeSpan.FromHours(count),
            "day" => TimeSpan.FromDays(count),
            "week" => TimeSpan.FromDays(count * DaysPerWeek),
            "month" => TimeSpan.FromDays(count * DaysPerMonth),
            _ => TimeSpan.FromDays(count * DaysPerYear),
        };
    }

    /// <summary>Parses docker time "2026-07-14 08:38:19 +0700 +07"; <c>null</c> when unrecognized.</summary>
    public static DateTimeOffset? ParseTimestamp(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[2].Length != OffsetLength || parts[2][0] is not ('+' or '-')
            || !DateTime.TryParseExact(parts[0] + " " + parts[1], TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || !int.TryParse(parts[2].AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[2].AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }

        var offset = new TimeSpan(hours, minutes, 0);
        return new DateTimeOffset(local, parts[2][0] == '-' ? -offset : offset);
    }

    /// <summary>Short form in the UI language: "45 h", "13 d", "2 wk", "3 mo", "2 y".</summary>
    public static string Format(TimeSpan duration)
    {
        var hours = (int)(duration.TotalHours + 0.5);
        var (count, unit) = duration.TotalSeconds < 60 ? (Math.Max(1, (int)duration.TotalSeconds), Strings.UnitSeconds)
            : duration.TotalMinutes < 60 ? ((int)duration.TotalMinutes, Strings.UnitMinutes)
            : hours < 2 * HoursPerDay ? (hours, Strings.UnitHours)
            : hours < 2 * DaysPerWeek * HoursPerDay ? (hours / HoursPerDay, Strings.UnitDays)
            : hours < 2 * DaysPerMonth * HoursPerDay ? (hours / HoursPerDay / DaysPerWeek, Strings.UnitWeeks)
            : hours < 2 * DaysPerYear * HoursPerDay ? (hours / HoursPerDay / DaysPerMonth, Strings.UnitMonths)
            : ((int)duration.TotalHours / HoursPerDay / DaysPerYear, Strings.UnitYears);
        return string.Format(CultureInfo.CurrentCulture, unit, count);
    }

    [GeneratedRegex(@"^(?:(?<count>\d+)|About an?|Less than a) (?<unit>second|minute|hour|day|week|month|year)s?$")]
    private static partial Regex Duration();
}
