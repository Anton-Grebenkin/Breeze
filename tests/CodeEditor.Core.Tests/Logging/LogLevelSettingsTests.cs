using CodeEditor.Core.Logging;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Tests.Logging;

public sealed class LogLevelSettingsTests
{
    [Fact]
    public void Setting_AppliesAtStartAndOnChange()
    {
        var options = new TestOptionsMonitor<LoggingOptions>(new LoggingOptions { Level = LogLevel.Warning });
        var levels = new LogLevelSwitch();
        var logger = new CollectingLogger<LogLevelSettings>();
        using var settings = new LogLevelSettings(options, levels, logger);

        settings.Start();
        Assert.Equal(LogLevel.Warning, levels.Minimum);
        Assert.False(levels.IsEnabled(LogLevel.Information));

        options.Set(new LoggingOptions { Level = LogLevel.Trace });
        Assert.True(levels.IsEnabled(LogLevel.Trace));
        Assert.False(levels.IsEnabled(LogLevel.None));
        Assert.Equal("Log level: Trace", logger.Entries[^1].Message);
    }

    [Fact]
    public void DefaultLevel_IsDebug() => Assert.Equal(LogLevel.Debug, new LoggingOptions().Level);
}
