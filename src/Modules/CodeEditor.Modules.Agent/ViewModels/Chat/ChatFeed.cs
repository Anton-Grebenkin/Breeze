using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// The chat feed for display (ADR 0010): session messages as is, but consecutive reasoning, progress, tool and check
/// messages grouped into one work block (<see cref="ActivityBlockViewModel"/>). The session keeps a flat list (history,
/// saving, tests); the feed follows its events so that regular adds and inserts touch only their own block and WPF does
/// not recreate what is shown. Any unexpected change triggers a full rebuild.
/// </summary>
public sealed class ChatFeed : IDisposable
{
    private readonly ObservableCollection<ChatMessageViewModel> _messages;
    private readonly Func<bool> _isBusy;
    private readonly TimeProvider _time;
    private readonly Dictionary<ChatMessageViewModel, ActivityBlockViewModel> _blocks = new(ReferenceEqualityComparer.Instance);
    private readonly ChainTail _tail = new();

    /// <param name="isBusy">Whether a turn is running; its blocks are expanded and timed.</param>
    public ChatFeed(ObservableCollection<ChatMessageViewModel> messages, Func<bool> isBusy, TimeProvider time)
    {
        _messages = messages;
        _isBusy = isBusy;
        _time = time;
        _messages.CollectionChanged += OnMessagesChanged;
        Rebuild();
    }

    /// <summary>Messages and work blocks (<see cref="ActivityBlockViewModel"/>).</summary>
    public ObservableCollection<object> Items { get; } = [];

    public static bool IsActivity(ChatMessageKind kind) =>
        kind is ChatMessageKind.Reasoning or ChatMessageKind.Progress or ChatMessageKind.Tool or ChatMessageKind.Status or ChatMessageKind.Review;

    /// <summary>Called every second to refresh the elapsed time in running blocks.</summary>
    public void Tick()
    {
        foreach (var block in Items.OfType<ActivityBlockViewModel>().Where(block => block.IsRunning))
        {
            block.Tick();
        }
    }

    public void Dispose()
    {
        _messages.CollectionChanged -= OnMessagesChanged;
        _tail.Dispose();
        DetachAll();
    }

    /// <summary>Called when a turn starts or ends; a finished turn collapses its blocks.</summary>
    public void OnBusyChanged()
    {
        if (!_isBusy())
        {
            foreach (var block in Items.OfType<ActivityBlockViewModel>().Where(block => block.IsRunning))
            {
                block.Finish();
            }
        }

        // When the turn ends, the answer under the collapsed block becomes a regular message at the same moment.
        UpdateTail();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var handled = e switch
        {
            { Action: NotifyCollectionChangedAction.Add, NewItems: [ChatMessageViewModel added] } => TryAdd(added, e.NewStartingIndex),
            { Action: NotifyCollectionChangedAction.Remove, OldItems: [ChatMessageViewModel removed] } => TryRemove(removed),
            _ => false,
        };

        if (!handled)
        {
            Rebuild();
        }

        UpdateTail();
    }

    // The live answer is last in the feed; the item above it decides how it is shown (ChainTail).
    private void UpdateTail()
    {
        var answer = _isBusy() && Items.Count >= 2 && Items[^1] is ChatMessageViewModel { Kind: ChatMessageKind.Assistant } last ? last : null;
        _tail.Update(answer, answer is null ? null : Items[^2]);
    }

    private bool TryAdd(ChatMessageViewModel message, int index)
    {
        var previous = index > 0 ? _messages[index - 1] : null;
        var next = index + 1 < _messages.Count ? _messages[index + 1] : null;
        if (!IsActivity(message.Kind))
        {
            // Regular messages are only appended; nothing inserts one into the middle of a block.
            return next is null && Append(message);
        }

        if (previous is not null && _blocks.TryGetValue(previous, out var block) && ReferenceEquals(block.Messages[^1], previous))
        {
            block.Add(message);
            _blocks[message] = block;
            return true;
        }

        // A new block goes after the previous message; one right before another block is unexpected.
        if (next is not null && _blocks.ContainsKey(next))
        {
            return false;
        }

        var created = new ActivityBlockViewModel(_time, _isBusy());
        created.Add(message);
        _blocks[message] = created;
        Items.Insert(previous is null ? 0 : Items.IndexOf(FeedItem(previous)) + 1, created);
        return true;
    }

    private bool Append(ChatMessageViewModel message)
    {
        Items.Add(message);
        return true;
    }

    private bool TryRemove(ChatMessageViewModel message)
    {
        if (!_blocks.Remove(message, out var block))
        {
            var index = Items.IndexOf(message);
            if (index < 0)
            {
                return false;
            }

            // The message separated two blocks that are now adjacent: rebuild.
            if (index > 0 && index < Items.Count - 1 && Items[index - 1] is ActivityBlockViewModel && Items[index + 1] is ActivityBlockViewModel)
            {
                return false;
            }

            Items.RemoveAt(index);
            return true;
        }

        block.Remove(message);
        if (block.Messages.Count == 0)
        {
            block.Detach();
            Items.Remove(block);
        }

        return true;
    }

    private object FeedItem(ChatMessageViewModel message) => _blocks.TryGetValue(message, out var block) ? block : message;

    private void Rebuild()
    {
        DetachAll();
        Items.Clear();
        _blocks.Clear();
        ActivityBlockViewModel? block = null;
        foreach (var message in _messages)
        {
            if (!IsActivity(message.Kind))
            {
                block = null;
                Items.Add(message);
                continue;
            }

            if (block is null)
            {
                block = new ActivityBlockViewModel(_time, live: false);
                Items.Add(block);
            }

            block.Add(message);
            _blocks[message] = block;
        }
    }

    private void DetachAll()
    {
        foreach (var block in Items.OfType<ActivityBlockViewModel>())
        {
            block.Detach();
        }
    }
}
