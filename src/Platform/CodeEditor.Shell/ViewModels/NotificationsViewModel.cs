using System.Collections.ObjectModel;
using CodeEditor.Core.Commands;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.ViewModels;

/// <summary>Notifications at the bottom left of the window, newest at the bottom.</summary>
public sealed class NotificationsViewModel(ICommandService commands) : INotificationService
{
    public ObservableCollection<NotificationViewModel> Items { get; } = [];

    public IDisposable Show(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var item = new NotificationViewModel(notification, commands, Remove);
        Items.Add(item);
        return new Removal(() => Remove(item));
    }

    private void Remove(NotificationViewModel item) => Items.Remove(item);

    private sealed class Removal(Action remove) : IDisposable
    {
        private Action? _remove = remove;

        public void Dispose() => Interlocked.Exchange(ref _remove, null)?.Invoke();
    }
}
