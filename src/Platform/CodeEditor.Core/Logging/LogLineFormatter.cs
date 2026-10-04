using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Logging;

/// <summary>
/// Log file line: <c>2026-09-27 12:30:45.123 +03:00 [INF] #1 CommandService: text</c>. The level is three letters
/// for searching (<c>[ERR]</c>), <c>#1</c> is the thread id (usually the UI thread). Continuation lines and the
/// exception stack are indented so entries are easy to tell apart.
/// </summary>
public static class LogLineFormatter
{
    private const string Indent = "    ";

    public static void Append(StringBuilder builder, in LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The interpolation handler formats straight into the builder without temporary strings.
        builder.Append(
            CultureInfo.InvariantCulture,
            $"{entry.Time:yyyy-MM-dd HH:mm:ss.fff zzz} [{LevelCode(entry.Level)}] #{entry.ThreadId} {entry.Category}: ");
        AppendIndented(builder, entry.Message);
        if (entry.Scopes is { } scopes)
        {
            builder.Append(" {").Append(scopes).Append('}');
        }

        builder.AppendLine();
        if (entry.Exception is { } exception)
        {
            builder.Append(Indent);
            AppendIndented(builder, exception);
            builder.AppendLine();
        }
    }

    public static string LevelCode(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    // Line breaks inside the text get an indent, so every entry starts with a date in the first column.
    private static void AppendIndented(StringBuilder builder, string text)
    {
        var start = 0;
        int newline;
        while ((newline = text.IndexOf('\n', start)) >= 0)
        {
            builder.Append(text.AsSpan(start, newline - start).TrimEnd('\r')).AppendLine().Append(Indent);
            start = newline + 1;
        }

        builder.Append(text.AsSpan(start).TrimEnd('\r'));
    }
}
