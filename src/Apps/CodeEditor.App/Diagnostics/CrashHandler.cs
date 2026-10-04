using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CodeEditor.App.Resources;
using CodeEditor.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CodeEditor.App.Diagnostics;

/// <summary>
/// Logs unhandled exceptions. A UI thread failure is logged and the app keeps running with a status bar message
/// (unsaved edits aren't lost), unless failures come in a burst: that's a loop (e.g. in markup) and the process exits.
/// A failure off the UI thread terminates the process after flushing the log. Unobserved task exceptions are logged.
/// </summary>
internal sealed partial class CrashHandler(ILogger<CrashHandler> logger, LogFileWriter logFile)
{
    /// <summary>More UI thread failures than this within <see cref="BurstWindow"/> is a loop and isn't swallowed.</summary>
    private const int MaxUiErrorsPerBurst = 10;

    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(2);

    private Action<string>? _notify;
    private long _burstStarted;
    private int _burstCount;

    public void AttachToProcess()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <param name="notify">Shows the user a message (status bar) after a UI thread failure.</param>
    public void AttachToDispatcher(Application application, Action<string> notify)
    {
        _notify = notify;
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogProcessCrash(logger, e.ExceptionObject as Exception, e.ExceptionObject.GetType().FullName ?? "?", e.IsTerminating);
        if (e.IsTerminating)
        {
            logFile.Complete(FlushTimeout);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnobservedTask(logger, e.Exception);
        e.SetObserved();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsBurst())
        {
            LogUiLoop(logger, e.Exception, MaxUiErrorsPerBurst, (int)BurstWindow.TotalSeconds);
            logFile.Complete(FlushTimeout);
            return;
        }

        LogUiError(logger, e.Exception);
        e.Handled = true;
        _notify?.Invoke(Strings.InternalError);
    }

    private bool IsBurst()
    {
        if (_burstCount == 0 || Stopwatch.GetElapsedTime(_burstStarted) > BurstWindow)
        {
            _burstStarted = Stopwatch.GetTimestamp();
            _burstCount = 0;
        }

        return ++_burstCount > MaxUiErrorsPerBurst;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception {Type}, process terminating: {IsTerminating}")]
    private static partial void LogProcessCrash(ILogger logger, Exception? exception, string type, bool isTerminating);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on the UI thread, continuing")]
    private static partial void LogUiError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "More than {Count} UI thread failures in {Seconds} s: a loop, process terminating")]
    private static partial void LogUiLoop(ILogger logger, Exception exception, int count, int seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unobserved task exception")]
    private static partial void LogUnobservedTask(ILogger logger, Exception exception);
}
