using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Wpf.Diagnostics;

/// <summary>
/// WPF binding errors are always code bugs (an empty menu, an invisible row): they are logged, and with
/// <c>CODEEDITOR_BINDING_ERRORS_FILE</c> set also appended to a file that UI tests check.
/// </summary>
public sealed partial class BindingErrorListener : TraceListener
{
    public const string FileVariable = "CODEEDITOR_BINDING_ERRORS_FILE";

    private readonly ILogger<BindingErrorListener> _logger;
    private readonly string? _file;
    private readonly Lock _lock = new();

    private BindingErrorListener(ILogger<BindingErrorListener> logger, string? file)
    {
        _logger = logger;
        _file = file;
    }

    /// <summary>Attaches the listener to the binding trace source, errors only.</summary>
    public static void Install(ILogger<BindingErrorListener> logger)
    {
        // Without Refresh the source isn't created unless a debugger is attached.
        PresentationTraceSources.Refresh();
        var source = PresentationTraceSources.DataBindingSource;
        source.Switch.Level = SourceLevels.Error;
        source.Listeners.Add(new BindingErrorListener(logger, Environment.GetEnvironmentVariable(FileVariable)));
    }

    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        LogBindingError(_logger, message);
        if (_file is null)
        {
            return;
        }

        // Diagnostics must not crash the app: an unavailable file leaves only the log entry above.
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_file, message + Environment.NewLine);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "WPF binding error: {Message}")]
    private static partial void LogBindingError(ILogger logger, string message);
}
