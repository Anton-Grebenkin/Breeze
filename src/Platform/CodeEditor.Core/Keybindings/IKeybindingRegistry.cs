namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Keybinding registry. Later registrations take precedence, so the user and modules override defaults.
/// </summary>
public interface IKeybindingRegistry
{
    event EventHandler? Changed;

    IDisposable Register(KeybindingDefinition binding);

    /// <summary>Bindings starting with the chord, in registration order. O(1) lookup.</summary>
    IReadOnlyList<KeybindingDefinition> GetByFirstChord(KeyChord chord);

    /// <summary>The command's latest binding, shown as a hint in the palette and menus.</summary>
    KeybindingDefinition? FindForCommand(string commandId);

    /// <summary>
    /// Hides already registered bindings of a command, all of them or only those with <paramref name="sequence"/>,
    /// like a <c>"-command"</c> rule in <c>keybindings.json</c>. Disposing restores them.
    /// </summary>
    IDisposable Suppress(string commandId, KeySequence? sequence = null);
}
