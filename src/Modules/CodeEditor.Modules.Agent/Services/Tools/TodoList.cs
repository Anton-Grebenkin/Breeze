using System.Collections.Immutable;
using System.Text;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// The agent's plan in the current chat, a task list: the model sends the whole list each time (simpler than per-item
/// edits), with at most one item in progress. The snapshot is immutable, so the panel and context read it from any
/// thread.
/// </summary>
public sealed class TodoList
{
    public const int SuggestedMinimum = 3;
    public const int SuggestedMaximum = 10;

    public ImmutableArray<TodoItem> Items { get; private set; } = [];

    /// <summary>The list changed (raised on the tool's background thread).</summary>
    public event EventHandler? Changed;

    public bool HasOpenItems => Items.Any(static item => item.Status != TodoStatus.Completed);

    public void Replace(IReadOnlyList<TodoItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = [.. items];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear() => Replace([]);

    /// <summary>The list for the model: <c>[x] 1. Done</c>, <c>[&gt;] 2. In progress</c>, <c>[ ] 3. Pending</c>.</summary>
    public string Render()
    {
        var text = new StringBuilder();
        foreach (var item in Items)
        {
            var mark = item.Status switch
            {
                TodoStatus.Completed => "[x]",
                TodoStatus.InProgress => "[>]",
                _ => "[ ]",
            };
            text.Append(mark).Append(' ').Append(item.Id).Append(". ").Append(item.Title).Append('\n');
        }

        return text.ToString().TrimEnd();
    }
}
