namespace CodeEditor.Core.Keybindings;

public enum KeyResolutionKind
{
    /// <summary>No binding; the key goes to the focused element.</summary>
    NotHandled,

    /// <summary>The first chord of a two-chord sequence was pressed; waiting for the second.</summary>
    WaitingForSecondChord,

    Command,

    /// <summary>The second chord is not bound; the key is swallowed.</summary>
    ChordNotFound,
}
