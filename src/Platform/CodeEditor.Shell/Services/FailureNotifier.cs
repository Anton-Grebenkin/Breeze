using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Threading;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.Services;

/// <summary>
/// Tells the user that something failed: a command whose handler threw, an error the UI thread caught. These used to
/// go to the status bar, a single line that the next message overwrites, so a failed command looked like nothing
/// happened. The notification stays until closed and offers the log; a new failure replaces the previous one.
/// </summary>
public sealed class FailureNotifier : IDisposable
{
    public const string NotificationId = "failure";

    private readonly ICommandService _commands;
    private readonly INotificationService _notifications;
    private readonly IUiDispatcher _dispatcher;
    private IDisposable? _shown;

    public FailureNotifier(ICommandService commands, INotificationService notifications, IUiDispatcher dispatcher)
    {
        _commands = commands;
        _notifications = notifications;
        _dispatcher = dispatcher;
        _commands.Failed += OnCommandFailed;
    }

    /// <summary>Shows a failure; safe from any thread.</summary>
    public void Show(string message) => _dispatcher.Post(() =>
    {
        _shown?.Dispose();
        _shown = _notifications.Show(new Notification(NotificationId, message, [new NotificationAction(Strings.OpenLog, LogCommands.OpenLogId)]));
    });

    public void Dispose()
    {
        _commands.Failed -= OnCommandFailed;
        _shown?.Dispose();
    }

    private void OnCommandFailed(object? sender, CommandFailedEventArgs e) =>
        Show(string.Format(CultureInfo.CurrentCulture, Strings.CommandFailed, e.Command.DisplayTitle, e.Exception.Message));
}
