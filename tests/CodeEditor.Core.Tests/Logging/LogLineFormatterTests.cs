using System.Text;
using CodeEditor.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Tests.Logging;

public sealed class LogLineFormatterTests
{
    private static readonly DateTimeOffset Time = new(2026, 9, 27, 12, 30, 45, 123, TimeSpan.FromHours(3));

    [Fact]
    public void Line_HasTimeLevelThreadCategoryAndMessage()
    {
        var text = Format(new LogEntry(Time, LogLevel.Warning, "CommandService", "Команда x не найдена", 1));

        Assert.Equal("2026-09-27 12:30:45.123 +03:00 [WRN] #1 CommandService: Команда x не найдена" + Environment.NewLine, text);
    }

    [Fact]
    public void MultilineMessageAndException_AreIndented()
    {
        var text = Format(new LogEntry(Time, LogLevel.Error, "Crash", "первая\r\nвторая", 7)
        {
            Exception = "System.Exception: сбой\n   at A.B()",
            Scopes = "чат 1",
        });

        var lines = text.Split(Environment.NewLine);
        Assert.Equal("2026-09-27 12:30:45.123 +03:00 [ERR] #7 Crash: первая", lines[0]);
        Assert.Equal("    вторая {чат 1}", lines[1]);
        Assert.Equal("    System.Exception: сбой", lines[2]);
        Assert.Equal("       at A.B()", lines[3]);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "TRC")]
    [InlineData(LogLevel.Debug, "DBG")]
    [InlineData(LogLevel.Information, "INF")]
    [InlineData(LogLevel.Critical, "CRT")]
    public void LevelCodes_AreThreeLetters(LogLevel level, string code) => Assert.Equal(code, LogLineFormatter.LevelCode(level));

    private static string Format(LogEntry entry)
    {
        var builder = new StringBuilder();
        LogLineFormatter.Append(builder, entry);
        return builder.ToString();
    }
}
