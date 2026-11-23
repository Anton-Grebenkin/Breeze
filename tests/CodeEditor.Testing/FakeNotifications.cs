using CodeEditor.Shell.Services;

namespace CodeEditor.Testing;

/// <summary>Notifications without a window: records what was shown.</summary>
public sealed class FakeNotifications : INotificationService
{
    public List<Notification> Shown { get; } = [];

    public IDisposable Show(Notification notification)
    {
        Shown.Add(notification);
        return new Closed();
    }

    private sealed class Closed : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
