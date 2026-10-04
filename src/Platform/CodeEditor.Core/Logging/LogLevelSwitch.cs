using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Logging;

/// <summary>
/// Minimum log file level, changeable at runtime (setting <c>log.level</c>). Exists before the DI container:
/// logging is needed from the first line of startup, while settings are read later.
/// </summary>
public sealed class LogLevelSwitch(LogLevel minimum = LogLevelSwitch.DefaultLevel)
{
    /// <summary>Debug by default: these entries show which commands ran before a crash.</summary>
    public const LogLevel DefaultLevel = LogLevel.Debug;

    private int _minimum = (int)minimum;

    public LogLevel Minimum
    {
        get => (LogLevel)Volatile.Read(ref _minimum);
        set => Volatile.Write(ref _minimum, (int)value);
    }

    public bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= Minimum;
}
