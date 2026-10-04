using System.Globalization;
using CodeEditor.Core.Output;
using CodeEditor.Modules.Output.Resources;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Output.Services;

/// <summary>
/// Writes <c>12:30:45.123 [Error] CommandService: text</c>, then the exception, if any.
/// Only Information and above reach the channel; debug messages stay in the debugger.
/// </summary>
internal sealed class OutputLogger(string category, Lazy<IOutputChannel> channel) : ILogger
{
    private const LogLevel MinLevel = LogLevel.Information;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= MinLevel && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        channel.Value.AppendLine($"{time} [{LevelName(logLevel)}] {category}: {formatter(state, exception)}");
        if (exception is not null)
        {
            channel.Value.AppendLine(exception.ToString());
        }
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Information => Strings.LevelInformation,
        LogLevel.Warning => Strings.LevelWarning,
        LogLevel.Error => Strings.LevelError,
        LogLevel.Critical => Strings.LevelCritical,
        _ => level.ToString(),
    };
}
