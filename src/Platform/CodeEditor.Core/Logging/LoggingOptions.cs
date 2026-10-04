using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Logging;

/// <summary>
/// The <c>log</c> settings section: <c>"log.level": "information"</c> sets log file verbosity
/// (<c>trace</c>, <c>debug</c>, <c>information</c>, <c>warning</c>, <c>error</c>). The Output panel shows
/// <c>information</c> and above regardless of this setting.
/// </summary>
public sealed class LoggingOptions
{
    public const string Section = "log";
    public const string LevelKey = "log.level";

    public LogLevel Level { get; set; } = LogLevelSwitch.DefaultLevel;
}
