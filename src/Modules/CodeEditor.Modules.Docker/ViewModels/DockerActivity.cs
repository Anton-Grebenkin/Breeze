using CodeEditor.Core.Output;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Docker.ViewModels;

/// <summary>
/// Docker panel action progress for the user: the status bar ("Stopping api…" → "api: done"), an error on the panel until
/// the next successful action or dismissal, and full docker output in the "Docker" channel of the Output panel (image
/// builds and pulls take minutes). Progress and results arrive on the UI thread; output lines on any thread.
/// </summary>
public sealed partial class DockerActivity(StatusBarViewModel statusBar, IOutputService output) : ObservableObject
{
    private IOutputChannel? _channel;

    /// <summary>Last action error; <c>null</c> when there is none or it was dismissed.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The "Docker" channel is created on the first action, so it is not listed before.</summary>
    private IOutputChannel Channel => _channel ??= output.GetOrCreate(Strings.OutputChannel);

    public void Started(string text, string commandLine)
    {
        statusBar.Message = text;
        Channel.AppendLine("> " + commandLine);
    }

    public void Succeeded(string text)
    {
        statusBar.Message = text;
        Error = null;
    }

    public void Failed(string text)
    {
        statusBar.Message = text;
        Error = text;
    }

    /// <summary>Writes a docker output line to the "Docker" channel, without terminal control sequences.</summary>
    public void Line(string line) => Channel.AppendLine(TerminalEscapes.Strip(line));

    /// <summary>A non-error message in the status bar ("ID copied").</summary>
    public void Inform(string text) => statusBar.Message = text;

    [RelayCommand]
    public void Dismiss() => Error = null;
}
