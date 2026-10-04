using System.Collections.Frozen;
using System.Reflection;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace CodeEditor.Modules.TextEditor.Wpf.Highlighting;

/// <summary>
/// Highlighting definitions by name (ADR 0036). AvalonEdit's built-ins are re-read from its resources; custom ones are
/// resources of this assembly (<c>Highlighting/Definitions/*.xshd</c>), named after the file. A custom definition
/// replaces a built-in one with the same name (JavaScript). The language catalog decides which definition a file uses.
/// </summary>
internal static class HighlightingDefinitions
{
    private const string BuiltInPrefix = "ICSharpCode.AvalonEdit.Highlighting.Resources.";
    private const string OwnPrefix = "Highlighting.";
    private const string XshdExtension = ".xshd";

    // Names match AvalonEdit's registration: nested definitions refer to them (HTML → JavaScript, C# → XmlDoc).
    private static readonly (string Name, string Resource)[] BuiltIn =
    [
        ("XmlDoc", "XmlDoc.xshd"),
        ("C#", "CSharp-Mode.xshd"),
        ("JavaScript", "JavaScript-Mode.xshd"),
        ("HTML", "HTML-Mode.xshd"),
        ("ASP/XHTML", "ASPX.xshd"),
        ("Boo", "Boo.xshd"),
        ("Coco", "Coco-Mode.xshd"),
        ("CSS", "CSS-Mode.xshd"),
        ("C++", "CPP-Mode.xshd"),
        ("Java", "Java-Mode.xshd"),
        ("Patch", "Patch-Mode.xshd"),
        ("PowerShell", "PowerShell.xshd"),
        ("PHP", "PHP-Mode.xshd"),
        ("Python", "Python-Mode.xshd"),
        ("TeX", "Tex-Mode.xshd"),
        ("TSQL", "TSQL-Mode.xshd"),
        ("VB", "VB-Mode.xshd"),
        ("XML", "XML-Mode.xshd"),
        ("MarkDown", "MarkDown-Mode.xshd"),
        ("Json", "Json.xshd"),
    ];

    private static readonly FrozenDictionary<string, (Assembly Assembly, string Resource)> Sources = CollectSources();

    public static IEnumerable<string> Names => Sources.Keys;

    /// <summary>Custom definitions of this assembly.</summary>
    public static IEnumerable<string> OwnNames => Sources.Where(source => source.Value.Assembly == OwnAssembly).Select(source => source.Key);

    private static Assembly OwnAssembly => typeof(HighlightingDefinitions).Assembly;

    /// <summary>
    /// A definition manager colored from the palette (theme token key → color). Definitions load lazily on the first
    /// file of their type; nested ones (HTML → JavaScript) come from the same manager and get the palette too.
    /// </summary>
    public static HighlightingManager CreateManager(IReadOnlyDictionary<string, Color> palette)
    {
        var manager = new HighlightingManager();
        foreach (var name in Sources.Keys)
        {
            manager.RegisterHighlighting(name, [], () => Load(name, manager, palette));
        }

        return manager;
    }

    /// <summary>The raw xshd definition, without theme colors.</summary>
    public static XshdSyntaxDefinition ReadXshd(string name)
    {
        var (assembly, resource) = Sources[name];
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Highlighting resource {resource} is missing.");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.LoadXshd(reader);
    }

    private static IHighlightingDefinition Load(string name, HighlightingManager manager, IReadOnlyDictionary<string, Color> palette)
    {
        var syntax = ReadXshd(name);
        foreach (var color in syntax.Elements.OfType<XshdColor>())
        {
            color.Background = null;
            color.Foreground = color.Name is { } colorName && SyntaxColorClassifier.ThemeKeyFor(colorName) is { } key && palette.TryGetValue(key, out var value)
                ? new SimpleHighlightingBrush(value)
                : null;
        }

        return HighlightingLoader.Load(syntax, manager);
    }

    private static FrozenDictionary<string, (Assembly Assembly, string Resource)> CollectSources()
    {
        var sources = BuiltIn.ToDictionary(
            definition => definition.Name,
            definition => (typeof(HighlightingManager).Assembly, BuiltInPrefix + definition.Resource),
            StringComparer.Ordinal);

        foreach (var resource in OwnAssembly.GetManifestResourceNames())
        {
            if (resource.StartsWith(OwnPrefix, StringComparison.Ordinal) && resource.EndsWith(XshdExtension, StringComparison.Ordinal))
            {
                sources[resource[OwnPrefix.Length..^XshdExtension.Length]] = (OwnAssembly, resource);
            }
        }

        return sources.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
