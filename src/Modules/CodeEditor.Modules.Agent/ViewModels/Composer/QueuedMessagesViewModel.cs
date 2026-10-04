using System.Collections.ObjectModel;
using CodeEditor.Core.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// Messages typed while the agent works (ADR 0026). Each goes to the model at the next step boundary and moves to the
/// feed; the remove button drops it earlier. The queue changes on the turn's thread, so the list is rebuilt on the UI
/// thread.
/// </summary>
public sealed partial class QueuedMessagesViewModel : ObservableObject, IDisposable
{
    private readonly UserMessageQueue _queue;
    private readonly IUiDispatcher _dispatcher;

    public QueuedMessagesViewModel(UserMessageQueue queue, IUiDispatcher dispatcher)
    {
        _queue = queue;
        _dispatcher = dispatcher;
        _queue.Changed += OnChanged;
    }

    public ObservableCollection<QueuedMessageViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public void Dispose() => _queue.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => _dispatcher.Post(Rebuild);

    private void Rebuild()
    {
        Items.Clear();
        foreach (var (index, message) in _queue.Items.Index())
        {
            Items.Add(new QueuedMessageViewModel(message, index, queued => _queue.Remove(queued)));
        }

        OnPropertyChanged(nameof(HasItems));
    }
}
