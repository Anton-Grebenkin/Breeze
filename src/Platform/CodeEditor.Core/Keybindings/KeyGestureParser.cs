namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Parses key gestures from text: <c>Ctrl+Shift+P</c>, <c>F1</c>, <c>Ctrl+K Ctrl+O</c>.
/// Case-insensitive; accepts aliases (<c>Esc</c>, <c>Del</c>, <c>PgUp</c>, <c>Control</c>).
/// </summary>
public static class KeyGestureParser
{
    private const int MaxChords = 2;
    private const string PlusKey = "+";
    private const string ModifiedPlusSuffix = "++";

    /// <exception cref="FormatException">Unknown key or invalid format.</exception>
    public static KeySequence Parse(string text) =>
        TryParse(text, out var sequence, out var error) ? sequence : throw new FormatException(error);

    public static bool TryParse(string text, out KeySequence sequence) => TryParse(text, out sequence, out _);

    private static bool TryParse(string text, out KeySequence sequence, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        sequence = default;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 0 or > MaxChords)
        {
            error = $"Expected one or two key chords: '{text}'.";
            return false;
        }

        if (!TryParseChord(parts[0], out var first, out error))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            sequence = new KeySequence(first);
            return true;
        }

        if (!TryParseChord(parts[1], out var second, out error))
        {
            return false;
        }

        sequence = new KeySequence(first, second);
        return true;
    }

    private static bool TryParseChord(string text, out KeyChord chord, out string? error)
    {
        chord = default;

        // In "+" and "Ctrl++" the last "+" is the key itself, not a separator.
        var isPlusKey = text == PlusKey || text.EndsWith(ModifiedPlusSuffix, StringComparison.Ordinal);
        var modifierText = isPlusKey ? text[..^Math.Min(text.Length, ModifiedPlusSuffix.Length)] : text;
        string[] parts = modifierText.Length == 0 ? [] : modifierText.Split('+');
        var keyName = isPlusKey ? PlusKey : parts[^1];
        var modifierNames = isPlusKey ? parts : parts[..^1];

        var modifiers = KeyModifiers.None;
        foreach (var name in modifierNames)
        {
            if (!KeyNames.TryGetModifier(name, out var modifier))
            {
                error = $"Unknown modifier '{name}' in '{text}'.";
                return false;
            }

            modifiers |= modifier;
        }

        if (!KeyNames.TryGetKey(keyName, out var key))
        {
            error = $"Unknown key '{keyName}' in '{text}'.";
            return false;
        }

        chord = new KeyChord(modifiers, key);
        error = null;
        return true;
    }
}
