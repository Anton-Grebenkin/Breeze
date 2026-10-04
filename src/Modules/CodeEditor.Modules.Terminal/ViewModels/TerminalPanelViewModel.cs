using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Pty;
using CodeEditor.Modules.Terminal.Services.Shells;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Terminal.ViewModels;

/// <summary>
/// The Terminal panel (bottom area, <c>Ctrl+`</c>): several terminals, one shown at a time, as in VS Code. The first
/// one starts when the panel is first shown; shells start in the open folder. Closing the app ends them.
/// </summary>
public sealed partial class TerminalPanelViewModel(
    TerminalProfiles profiles,
    ITerminalProcessFactory processes,
    IOptionsMonitor<TerminalOptions> options,
    IWorkspace workspace,
    IContextKeyService context,
    IUiDispatcher ui,
    StatusBarViewModel statusBar,
    ILogger<TerminalPanelViewModel> logger) : ObservableObject, IFocusableContent, IDisposable
{
    /// <summary>Set while a terminal has keyboard focus: keys go to the shell (<see cref="Commands.TerminalKeys"/>).</summary>
    public const string FocusContextKey = "terminalFocus";

    // The view reports the real size as soon as it lays the terminal out.
    private const int InitialColumns = 120;
    private const int InitialRows = 30;

    private int _lastId;

    public ObservableCollection<TerminalSessionViewModel> Sessions { get; } = [];

    [ObservableProperty]
    public partial TerminalSessionViewModel? Active { get; set; }

    public double FontSize => options.CurrentValue.FontSize;

    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The panel was shown: a first terminal starts, as in VS Code.</summary>
    public void EnsureStarted()
    {
        if (Sessions.Count == 0)
        {
            Start();
        }
    }

    /// <summary>Starts a terminal: the default shell unless given, in the open folder unless given.</summary>
    public TerminalSessionViewModel? Start(TerminalProfile? profile = null, string? folder = null)
    {
        if ((profile ?? profiles.Default(options.CurrentValue.DefaultProfile)) is not { } shell)
        {
            statusBar.Message = Strings.NoShellFound;
            return null;
        }

        var directory = folder ?? workspace.Root ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        try
        {
            var process = processes.Start(new TerminalLaunch(shell.CommandLine, directory, InitialColumns, InitialRows));
            var session = new TerminalSessionViewModel(++_lastId, shell, process, ui);
            Sessions.Add(session);
            Active = session;
            LogStarted(logger, shell.Id, process.ProcessId, directory);
            return session;
        }
        catch (Win32Exception exception)
        {
            LogStartFailed(logger, exception, shell.CommandLine);
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.TerminalStartFailed, shell.Name, exception.Message);
            return null;
        }
    }

    /// <summary>Ends the shell and closes its terminal; the neighbor becomes active.</summary>
    public void Kill(TerminalSessionViewModel? session = null)
    {
        if ((session ?? Active) is not { } target)
        {
            return;
        }

        var index = Sessions.IndexOf(target);
        target.Dispose();
        Sessions.Remove(target);
        if (ReferenceEquals(Active, target))
        {
            Active = Sessions.Count == 0 ? null : Sessions[Math.Min(index, Sessions.Count - 1)];
        }
    }

    public void Clear() => Active?.Clear();

    [RelayCommand]
    private void NewTerminal() => Start();

    [RelayCommand]
    private void KillActive() => Kill();

    [RelayCommand]
    private void ClearActive() => Clear();

    /// <summary>The view reports keyboard focus: while set, keys go to the shell.</summary>
    public void SetFocused(bool focused) => context.Set(FocusContextKey, focused);

    public void Dispose()
    {
        foreach (var session in Sessions)
        {
            session.Dispose();
        }

        Sessions.Clear();
        context.Set(FocusContextKey, false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Terminal started: {Profile}, process {ProcessId}, in {Folder}")]
    private static partial void LogStarted(ILogger logger, string profile, int processId, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Terminal did not start: {CommandLine}")]
    private static partial void LogStartFailed(ILogger logger, Exception exception, string commandLine);
}
