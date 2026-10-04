namespace CodeEditor.Core.Keybindings;

/// <summary>
/// One chord: modifiers and a key, e.g. <c>Ctrl+Shift+P</c>. Two bytes; equality and hashing do not allocate.
/// </summary>
public readonly record struct KeyChord(KeyModifiers Modifiers, KeyCode Key)
{
    public override string ToString() => KeyNames.Format(this);
}
