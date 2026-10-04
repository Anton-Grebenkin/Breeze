using CodeEditor.Core.Common;

namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Indexes bindings by first chord and by command. Not thread-safe: used from the UI thread.
/// </summary>
public sealed class KeybindingRegistry : IKeybindingRegistry
{
    private readonly Dictionary<KeyChord, List<KeybindingDefinition>> _byFirstChord = [];
    private readonly Dictionary<string, List<KeybindingDefinition>> _byCommand = new(StringComparer.Ordinal);

    // By reference: records compare by value, but a specific registration is hidden.
    private readonly HashSet<KeybindingDefinition> _hidden = new(ReferenceEqualityComparer.Instance);

    public event EventHandler? Changed;

    public IDisposable Register(KeybindingDefinition binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.CommandId);

        AddToIndexes(binding);
        Changed?.Invoke(this, EventArgs.Empty);

        return new DisposableAction(() =>
        {
            // A binding hidden by a rule is already out of the indexes; just forget it.
            if (!_hidden.Remove(binding))
            {
                RemoveFromIndexes(binding);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    public IDisposable Suppress(string commandId, KeySequence? sequence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        KeybindingDefinition[] matched = _byCommand.TryGetValue(commandId, out var bindings)
            ? [.. bindings.Where(binding => sequence is null || binding.Sequence == sequence)]
            : [];
        foreach (var binding in matched)
        {
            RemoveFromIndexes(binding);
            _hidden.Add(binding);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new DisposableAction(() =>
        {
            foreach (var binding in matched.Where(_hidden.Remove))
            {
                AddToIndexes(binding);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    // Array.Empty, not []: [] as IReadOnlyList allocates a wrapper, and this runs on every key press.
    public IReadOnlyList<KeybindingDefinition> GetByFirstChord(KeyChord chord) =>
        _byFirstChord.TryGetValue(chord, out var bindings) ? bindings : Array.Empty<KeybindingDefinition>();

    public KeybindingDefinition? FindForCommand(string commandId) =>
        _byCommand.TryGetValue(commandId, out var bindings) ? bindings[^1] : null;

    private void AddToIndexes(KeybindingDefinition binding)
    {
        Add(_byFirstChord, binding.Sequence.First, binding);
        Add(_byCommand, binding.CommandId, binding);
    }

    private void RemoveFromIndexes(KeybindingDefinition binding)
    {
        Remove(_byFirstChord, binding.Sequence.First, binding);
        Remove(_byCommand, binding.CommandId, binding);
    }

    private static void Add<TKey>(Dictionary<TKey, List<KeybindingDefinition>> index, TKey key, KeybindingDefinition binding)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var bindings))
        {
            bindings = [];
            index[key] = bindings;
        }

        bindings.Add(binding);
    }

    private static void Remove<TKey>(Dictionary<TKey, List<KeybindingDefinition>> index, TKey key, KeybindingDefinition binding)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var bindings))
        {
            return;
        }

        // By reference: records compare by value, but exactly this registration must be removed.
        var position = bindings.FindLastIndex(candidate => ReferenceEquals(candidate, binding));
        if (position >= 0)
        {
            bindings.RemoveAt(position);
        }

        if (bindings.Count == 0)
        {
            index.Remove(key);
        }
    }
}
