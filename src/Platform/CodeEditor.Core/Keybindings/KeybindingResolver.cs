using CodeEditor.Core.Context;

namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Turns pressed chords into commands, handling two-chord sequences and <c>when</c> conditions.
/// Keeps the pending chord state; not thread-safe: used from the UI thread.
/// </summary>
/// <remarks>
/// Candidate lookup is O(1) by first chord, then O(k) over that chord's bindings from last to first:
/// later registrations take precedence.
/// </remarks>
public sealed class KeybindingResolver(IKeybindingRegistry registry)
{
    private KeyChord? _pendingChord;

    /// <summary>The first chord while waiting for the second one.</summary>
    public KeyChord? PendingChord => _pendingChord;

    public KeyResolution Resolve(KeyChord chord, IContextKeyLookup context) =>
        _pendingChord is { } first ? ResolveSecond(first, chord, context) : ResolveFirst(chord, context);

    /// <summary>Drops an unfinished sequence, e.g. when the window loses focus.</summary>
    public void Reset() => _pendingChord = null;

    private KeyResolution ResolveFirst(KeyChord chord, IContextKeyLookup context)
    {
        var candidates = registry.GetByFirstChord(chord);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var binding = candidates[i];
            if (!IsEnabled(binding, context))
            {
                continue;
            }

            if (!binding.Sequence.IsChord)
            {
                return KeyResolution.Command(binding);
            }

            _pendingChord = chord;
            return KeyResolution.Waiting(chord);
        }

        return KeyResolution.NotHandled;
    }

    private KeyResolution ResolveSecond(KeyChord first, KeyChord second, IContextKeyLookup context)
    {
        _pendingChord = null;

        var candidates = registry.GetByFirstChord(first);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var binding = candidates[i];
            if (binding.Sequence.Second == second && IsEnabled(binding, context))
            {
                return KeyResolution.Command(binding);
            }
        }

        return KeyResolution.ChordNotFound(first);
    }

    private static bool IsEnabled(KeybindingDefinition binding, IContextKeyLookup context) =>
        !binding.DisplayOnly && (binding.When?.Evaluate(context) ?? true);
}
