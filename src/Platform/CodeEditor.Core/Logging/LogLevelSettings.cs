using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Core.Logging;

/// <summary>Applies the <c>log.level</c> setting to <see cref="LogLevelSwitch"/> at startup and on change.</summary>
public sealed partial class LogLevelSettings(IOptionsMonitor<LoggingOptions> options, LogLevelSwitch levels, ILogger<LogLevelSettings> logger) : IDisposable
{
    private IDisposable? _subscription;

    public void Start()
    {
        Apply(options.CurrentValue);
        _subscription = options.OnChange(Apply);
    }

    public void Dispose() => _subscription?.Dispose();

    private void Apply(LoggingOptions value)
    {
        if (value.Level == levels.Minimum)
        {
            return;
        }

        levels.Minimum = value.Level;
        LogLevelChanged(logger, value.Level);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Log level: {Level}")]
    private static partial void LogLevelChanged(ILogger logger, LogLevel level);
}
