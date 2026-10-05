using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Settings;
using Microsoft.Extensions.Options;

namespace CodeEditor.Shell.Integration;

/// <summary>
/// Asks whether to make Breeze the app for code files (<see cref="WindowsIntegrationOptions.FileTypes"/>) until the
/// user answers. The answer is a setting, so it closes the question in every window; closing the bar without an
/// answer asks again at the next start.
/// </summary>
public sealed class FileTypesPrompt(
    INotificationService notifications,
    IWindowsIntegration integration,
    IOptionsMonitor<WindowsIntegrationOptions> options) : IDisposable
{
    public const string NotificationId = "fileTypes";

    private IDisposable? _notification;
    private IDisposable? _subscription;

    public void ShowIfUndecided()
    {
        if (!integration.IsAvailable || options.CurrentValue.FileTypes is not null || _notification is not null)
        {
            return;
        }

        _notification = notifications.Show(new Notification(NotificationId, Strings.FileTypesQuestion,
        [
            new NotificationAction(Strings.FileTypesNo, WindowsIntegrationCommands.DisableFileTypesId),
            new NotificationAction(Strings.FileTypesYes, WindowsIntegrationCommands.EnableFileTypesId, IsPrimary: true),
        ]));
        _subscription = options.OnChange(changed =>
        {
            if (changed.FileTypes is not null)
            {
                Dispose();
            }
        });
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        _notification?.Dispose();
    }
}
