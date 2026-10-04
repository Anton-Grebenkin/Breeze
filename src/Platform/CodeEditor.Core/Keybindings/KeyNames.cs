using System.Collections.Frozen;
using System.Text;

namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Key and modifier names for parsing and display. Lookup tables are built once.
/// </summary>
internal static class KeyNames
{
    private const char Separator = '+';

    private static readonly (KeyModifiers Modifier, string Name)[] ModifierOrder =
    [
        (KeyModifiers.Ctrl, "Ctrl"),
        (KeyModifiers.Shift, "Shift"),
        (KeyModifiers.Alt, "Alt"),
        (KeyModifiers.Win, "Win"),
    ];

    private static readonly FrozenDictionary<KeyCode, string> DisplayNames = BuildDisplayNames();

    private static readonly FrozenDictionary<string, KeyCode> KeysByName = BuildKeysByName();

    private static readonly FrozenDictionary<string, KeyModifiers> ModifiersByName =
        new Dictionary<string, KeyModifiers>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = KeyModifiers.Ctrl,
            ["Control"] = KeyModifiers.Ctrl,
            ["Shift"] = KeyModifiers.Shift,
            ["Alt"] = KeyModifiers.Alt,
            ["Win"] = KeyModifiers.Win,
            ["Meta"] = KeyModifiers.Win,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetKey(string name, out KeyCode key) => KeysByName.TryGetValue(name, out key);

    public static bool TryGetModifier(string name, out KeyModifiers modifier) =>
        ModifiersByName.TryGetValue(name, out modifier);

    public static string Format(KeyChord chord)
    {
        var builder = new StringBuilder();
        foreach (var (modifier, name) in ModifierOrder)
        {
            if (chord.Modifiers.HasFlag(modifier))
            {
                builder.Append(name).Append(Separator);
            }
        }

        return builder.Append(DisplayNames.GetValueOrDefault(chord.Key, chord.Key.ToString())).ToString();
    }

    private static FrozenDictionary<KeyCode, string> BuildDisplayNames()
    {
        var names = new Dictionary<KeyCode, string>
        {
            [KeyCode.Escape] = "Esc",
            [KeyCode.Delete] = "Del",
            [KeyCode.Insert] = "Ins",
            [KeyCode.Semicolon] = ";",
            [KeyCode.EqualsSign] = "=",
            [KeyCode.Comma] = ",",
            [KeyCode.Minus] = "-",
            [KeyCode.Period] = ".",
            [KeyCode.Slash] = "/",
            [KeyCode.Backquote] = "`",
            [KeyCode.BracketLeft] = "[",
            [KeyCode.Backslash] = "\\",
            [KeyCode.BracketRight] = "]",
            [KeyCode.Quote] = "'",
        };

        for (var digit = KeyCode.D0; digit <= KeyCode.D9; digit++)
        {
            names[digit] = ((char)('0' + (digit - KeyCode.D0))).ToString();
        }

        return names.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, KeyCode> BuildKeysByName()
    {
        var keys = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            ["Esc"] = KeyCode.Escape,
            ["Del"] = KeyCode.Delete,
            ["Ins"] = KeyCode.Insert,
            ["Return"] = KeyCode.Enter,
            ["PgUp"] = KeyCode.PageUp,
            ["PgDn"] = KeyCode.PageDown,
            ["Plus"] = KeyCode.EqualsSign,
            ["+"] = KeyCode.EqualsSign,
        };

        foreach (var key in Enum.GetValues<KeyCode>())
        {
            if (key != KeyCode.None)
            {
                keys.TryAdd(key.ToString(), key);
            }
        }

        // Display names ("1", "/", "[") parse too.
        foreach (var (key, name) in DisplayNames)
        {
            keys.TryAdd(name, key);
        }

        return keys.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
