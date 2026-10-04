using System.Globalization;
using CodeEditor.Core.Output;
using CodeEditor.Core.Processes;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Tools.Resources;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>Tool runs in the "Tools" Output channel and the status bar, whoever started them.</summary>
public sealed class ToolActivity(IOutputService output, StatusBarViewModel statusBar, IUiDispatcher dispatcher)
{
    private IOutputChannel? _channel;

    private IOutputChannel Channel => _channel ??= output.GetOrCreate(Strings.OutputChannel);

    public void Started(string tool, string commandLine)
    {
        Channel.AppendLine("> " + commandLine);
        Status(Format(Strings.ToolStarted, tool));
    }

    /// <summary>Output line; comes from a background thread.</summary>
    public void Line(string line) => Channel.AppendLine(line);

    public void Finished(string tool, ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var message = result.TimedOut ? Format(Strings.ToolTimedOutStatus, tool)
            : result.ExitCode == 0 ? Format(Strings.ToolFinished, tool)
            : Format(Strings.ToolFailedStatus, tool, result.ExitCode);
        Channel.AppendLine(message);
        Status(message);
    }

    public void Failed(string tool, string error)
    {
        var message = Format(Strings.ToolNotStarted, tool, error);
        Channel.AppendLine(message);
        Status(message);
    }

    private void Status(string message) => dispatcher.Post(() => statusBar.Message = message);

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
