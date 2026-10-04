namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Result of resolving a chord in <see cref="KeybindingResolver"/>.
/// </summary>
public readonly record struct KeyResolution(
    KeyResolutionKind Kind,
    KeybindingDefinition? Binding = null,
    KeyChord? PendingChord = null)
{
    public static KeyResolution NotHandled => new(KeyResolutionKind.NotHandled);

    public static KeyResolution Command(KeybindingDefinition binding) => new(KeyResolutionKind.Command, binding);

    public static KeyResolution Waiting(KeyChord first) => new(KeyResolutionKind.WaitingForSecondChord, PendingChord: first);

    public static KeyResolution ChordNotFound(KeyChord first) => new(KeyResolutionKind.ChordNotFound, PendingChord: first);
}
