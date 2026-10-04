using System.Collections.Frozen;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.TextEditor.Wpf.Highlighting;

/// <summary>
/// Maps a named color from a highlighting definition to a theme token. Custom definitions (ADR 0036) name colors after
/// tokens: "Comment", "ControlKeyword", "Attribute", "Variable"; "Text" is the editor text color, used inside strings
/// (<c>${…}</c> in templates). AvalonEdit built-ins use their own names ("GotoKeywords", "MethodCall",
/// "AttributeValue"…), matched by name markers. Rule order matters: "AttributeValue" is a string, not an attribute.
/// </summary>
internal static class SyntaxColorClassifier
{
    private static readonly FrozenDictionary<string, string> TokenNames = new Dictionary<string, string>
    {
        ["Comment"] = ThemeKeys.SyntaxComment,
        ["String"] = ThemeKeys.SyntaxString,
        ["Number"] = ThemeKeys.SyntaxNumber,
        ["Keyword"] = ThemeKeys.SyntaxKeyword,
        ["ControlKeyword"] = ThemeKeys.SyntaxControlKeyword,
        ["Function"] = ThemeKeys.SyntaxFunction,
        ["Type"] = ThemeKeys.SyntaxType,
        ["Tag"] = ThemeKeys.SyntaxTag,
        ["Attribute"] = ThemeKeys.SyntaxAttribute,
        ["Variable"] = ThemeKeys.SyntaxVariable,
        ["Preprocessor"] = ThemeKeys.SyntaxPreprocessor,
        ["Text"] = ThemeKeys.EditorForeground,
        ["Error"] = ThemeKeys.ErrorForeground,
        ["Warning"] = ThemeKeys.WarningForeground,
        ["Success"] = ThemeKeys.SuccessForeground,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Diff added/removed lines use number/string colors, like markup.inserted/markup.deleted in VS Code.
    private static readonly (string[] Markers, string ThemeKey)[] Rules =
    [
        (["comment", "xmldoc"], ThemeKeys.SyntaxComment),
        (["string", "char", "attributevalue", "cdata", "code", "removed"], ThemeKeys.SyntaxString),
        (["number", "digit", "date", "added"], ThemeKeys.SyntaxNumber),
        (["preprocessor", "directive", "doctype", "xmldeclaration", "pragma"], ThemeKeys.SyntaxPreprocessor),
        (["goto", "exception", "flow", "loop", "conditional", "jump", "selection", "iteration"], ThemeKeys.SyntaxControlKeyword),
        (["method", "function", "command"], ThemeKeys.SyntaxFunction),
        (["property", "fieldname", "variable"], ThemeKeys.SyntaxVariable),
        (["attributename", "attribute"], ThemeKeys.SyntaxAttribute),
        (["tag", "entity", "entities"], ThemeKeys.SyntaxTag),
        (["keyword", "modifier", "visibility", "truefalse", "null", "this", "namespace", "operator", "getset", "semantic",
          "checked", "unsafe", "parameter", "heading", "reserved", "builtin", "literal", "void", "package", "bool", "friend",
          "control", "header", "filename"], ThemeKeys.SyntaxKeyword),
        (["type", "class", "link"], ThemeKeys.SyntaxType),
    ];

    /// <summary>All tokens the classifier can produce; the theme palette is built from them.</summary>
    public static IEnumerable<string> PaletteKeys => TokenNames.Values.Concat(Rules.Select(rule => rule.ThemeKey)).Distinct();

    /// <summary>The theme token key, or <c>null</c> for the default text color (e.g. punctuation).</summary>
    public static string? ThemeKeyFor(string colorName)
    {
        if (TokenNames.TryGetValue(colorName, out var token))
        {
            return token;
        }

        foreach (var (markers, themeKey) in Rules)
        {
            if (markers.Any(marker => colorName.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                return themeKey;
            }
        }

        return null;
    }
}
