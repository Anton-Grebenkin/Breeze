using System.Collections.Frozen;
using System.Globalization;
using System.IO;

namespace CodeEditor.UI.Themes;

/// <summary>
/// Codicon glyphs by name (<c>search</c>, <c>files</c>, <c>folder-opened</c>), as icons are named in VS Code. Modules
/// refer to icons by name and don't depend on the font. The table is read once from an assembly resource.
/// </summary>
public static class Codicons
{
    /// <summary>Icon for unknown names: noticeable but harmless.</summary>
    public const string Fallback = "question";

    private const string ResourceName = "CodeEditor.UI.Codicons.csv";

    private static readonly Lazy<FrozenDictionary<string, string>> Map = new(Load);

    public static string Glyph(string? name) =>
        name is not null && Map.Value.TryGetValue(name, out var glyph) ? glyph : Map.Value[Fallback];

    public static bool Contains(string name) => Map.Value.ContainsKey(name);

    // Format: short_name,character,unicode; takes the name and the last column (hex code point).
    private static FrozenDictionary<string, string> Load()
    {
        using var stream = typeof(Codicons).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Resource {ResourceName} not found.");
        using var reader = new StreamReader(stream);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            var name = line[..line.IndexOf(',', StringComparison.Ordinal)];
            var code = int.Parse(line.AsSpan(line.LastIndexOf(',') + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            map[name] = char.ConvertFromUtf32(code);
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
