namespace CodeEditor.Shell.Services;

/// <summary>
/// Notifications that ask the user something without interrupting: they stay until a button runs its command, the
/// user closes them, or the caller disposes the result. Call on the UI thread.
/// </summary>
public interface INotificationService
{
    IDisposable Show(Notification notification);
}
