using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using CodeEditor.App.Resources;
using CodeEditor.Shell.ViewModels;
using Microsoft.Extensions.Logging;

namespace CodeEditor.App.Startup;

/// <summary>
/// After the window's first render, writes the startup time to the log and status bar, and the phases to the file in
/// <c>CODEEDITOR_STARTUP_TRACE</c> if set (for budget measurements).
/// </summary>
internal sealed partial class StartupReporter(
    StartupClock clock,
    StatusBarViewModel statusBar,
    ILogger<StartupReporter> logger)
{
    private const string TraceVariable = "CODEEDITOR_STARTUP_TRACE";

    public void Attach(Window window) => window.ContentRendered += OnContentRendered;

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.ContentRendered -= OnContentRendered;
        }

        clock.Mark("rendered");
        var sinceMain = clock.SinceMain;
        var total = StartupClock.SinceProcessStart();

        LogStartup(logger, (long)total.TotalMilliseconds, (long)sinceMain.TotalMilliseconds);
        statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.StartupTime, (long)total.TotalMilliseconds);
        WriteTrace(total);
    }

    private void WriteTrace(TimeSpan total)
    {
        var path = Environment.GetEnvironmentVariable(TraceVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"process-to-render\t{total.TotalMilliseconds:F0}");
        foreach (var (phase, elapsed) in clock.Phases)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{phase}\t{elapsed.TotalMilliseconds:F0}");
        }

        File.WriteAllText(path, text.ToString());
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Window rendered {TotalMs} ms after process start ({SinceMainMs} ms after entering Main)")]
    private static partial void LogStartup(ILogger logger, long totalMs, long sinceMainMs);
}
