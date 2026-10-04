namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Key gesture: one chord or a sequence of two, e.g. <c>Ctrl+K Ctrl+O</c>.
/// </summary>
public readonly record struct KeySequence(KeyChord First, KeyChord? Second = null)
{
    public bool IsChord => Second.HasValue;

    public static KeySequence Parse(string text) => KeyGestureParser.Parse(text);

    public override string ToString() => Second is { } second ? $"{First} {second}" : First.ToString();
}
