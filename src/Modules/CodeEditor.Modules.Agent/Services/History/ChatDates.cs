using System.Globalization;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>
/// When a chat happened, in human terms and the UI language format: "today, 14:32", "yesterday, 9:10", "September 12",
/// "9/12/2025".
/// </summary>
public static class ChatDates
{
    public static string Describe(DateTimeOffset moment, DateTimeOffset now)
    {
        var culture = CultureInfo.CurrentUICulture;
        var local = moment.ToOffset(now.Offset);
        var day = local.Date;
        var time = local.ToString("t", culture);
        if (day == now.Date)
        {
            return string.Format(culture, Strings.ChatToday, time);
        }

        if (day == now.Date.AddDays(-1))
        {
            return string.Format(culture, Strings.ChatYesterday, time);
        }

        return day.Year == now.Year ? day.ToString("M", culture) : day.ToString("d", culture);
    }
}
