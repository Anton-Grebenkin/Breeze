using System.Collections.Frozen;
using CodeEditor.Modules.Documents.Model;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>Word run and paragraph properties to document model styles and alignment.</summary>
internal static class WordFormatting
{
    /// <summary>Monospace fonts: text in them is code.</summary>
    private static readonly FrozenSet<string> MonospaceFonts = FrozenSet.ToFrozenSet(
        ["Consolas", "Courier New", "Courier", "Cascadia Mono", "Cascadia Code", "Lucida Console", "Source Code Pro", "JetBrains Mono",
            "Fira Code", "Fira Mono", "Menlo", "Monaco", "DejaVu Sans Mono", "Liberation Mono", "Roboto Mono", "PT Mono"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Run style: the character style with direct formatting on top.</summary>
    public static TextStyle Style(RunProperties? properties, WordStyleSheet styles)
    {
        var style = styles.CharacterStyle(properties?.RunStyle?.Val?.Value);
        if (properties is null)
        {
            return style;
        }

        style = Apply(style, properties.Bold, TextStyle.Bold);
        style = Apply(style, properties.Italic, TextStyle.Italic);
        if (properties.Underline?.Val?.Value is { } underline && underline != UnderlineValues.None)
        {
            style |= TextStyle.Underline;
        }

        if (IsOn(properties.Strike) || IsOn(properties.DoubleStrike))
        {
            style |= TextStyle.Strike;
        }

        return IsMonospace(properties.RunFonts) ? style | TextStyle.Code : style;
    }

    /// <summary>On/off flag: an element without a value sets it, "false" clears it, no element leaves it as is.</summary>
    public static TextStyle Apply(TextStyle style, OnOffType? element, TextStyle flag) => element switch
    {
        null => style,
        _ when IsOn(element) => style | flag,
        _ => style & ~flag,
    };

    public static bool IsMonospace(RunFonts? fonts) =>
        fonts?.Ascii?.Value is { } ascii ? MonospaceFonts.Contains(ascii) : fonts?.HighAnsi?.Value is { } high && MonospaceFonts.Contains(high);

    public static BlockAlignment Alignment(ParagraphProperties? properties)
    {
        if (properties?.Justification?.Val?.Value is not { } justification)
        {
            return BlockAlignment.Left;
        }

        if (justification == JustificationValues.Center)
        {
            return BlockAlignment.Center;
        }

        if (justification == JustificationValues.Right || justification == JustificationValues.End)
        {
            return BlockAlignment.Right;
        }

        return justification == JustificationValues.Both || justification == JustificationValues.Distribute ? BlockAlignment.Justify : BlockAlignment.Left;
    }

    private static bool IsOn(OnOffType? element) => element is not null && (element.Val?.Value ?? true);
}
