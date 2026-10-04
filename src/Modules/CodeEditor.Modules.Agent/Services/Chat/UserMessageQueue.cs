using System.Diagnostics.CodeAnalysis;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// User messages sent while the agent works (ADR 0026), as in Codex, Copilot and Cursor. The input is not blocked: a
/// message waits for the next step boundary. The tool loop ends after the current calls
/// (<see cref="GuardedToolFunction"/>) and the message goes to the model as a regular user message
/// (<see cref="Workflow.ChecksExecutor"/>). Read by both the tool thread and the UI thread.
/// </summary>
public sealed class UserMessageQueue
{
    private readonly Lock _gate = new();
    private readonly List<QueuedMessage> _items = [];

    /// <summary>The queue changed; raised on any thread.</summary>
    public event EventHandler? Changed;

    public bool HasPending
    {
        get
        {
            lock (_gate)
            {
                return _items.Count > 0;
            }
        }
    }

    public IReadOnlyList<QueuedMessage> Items
    {
        get
        {
            lock (_gate)
            {
                return [.. _items];
            }
        }
    }

    public void Enqueue(QueuedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            _items.Add(message);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The user changed their mind: removes a message not yet sent to the model.</summary>
    public bool Remove(QueuedMessage message)
    {
        bool removed;
        lock (_gate)
        {
            removed = _items.Remove(message);
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    public bool TryDequeue([NotNullWhen(true)] out QueuedMessage? message)
    {
        lock (_gate)
        {
            message = _items.FirstOrDefault();
            if (message is null)
            {
                return false;
            }

            _items.RemoveAt(0);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Takes everything not yet sent to the model: the turn stopped and the texts go back to the input.</summary>
    public IReadOnlyList<QueuedMessage> TakeAll()
    {
        List<QueuedMessage> taken;
        lock (_gate)
        {
            taken = [.. _items];
            _items.Clear();
        }

        if (taken.Count > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return taken;
    }
}
