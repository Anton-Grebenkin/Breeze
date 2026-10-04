using System.Windows;
using System.Windows.Media;
using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.Shell.Theming;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeEditor.Modules.TextEditor.Wpf.Highlighting;

/// <summary>
/// Syntax highlighting in theme colors. AvalonEdit's built-in definitions assume a light background, so they are
/// re-read from the source xshd with <c>Brush.Syntax.*</c> token colors, like the custom ones (ADR 0036). Each theme
/// has its own definition manager, so nested languages (HTML → JavaScript, CSS) get theme colors too. Definitions
/// load lazily on the first file of their type; the file's language comes from the language catalog.
/// </summary>
public sealed class ThemedHighlighting
{
    private readonly Application _application;
    private readonly IThemeService _themes;
    private readonly Dictionary<ThemeKind, HighlightingManager> _managers = [];

    public ThemedHighlighting(Application application, IThemeService themes)
    {
        _application = application;
        _themes = themes;
        _themes.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The theme changed; open editors must fetch their definition again.</summary>
    public event EventHandler? Changed;

    public IHighlightingDefinition? ForFile(string filePath) => For(LanguageCatalog.ForFile(filePath));

    /// <summary>The language's definition in the current theme colors; <c>null</c> for plain text.</summary>
    public IHighlightingDefinition? For(Language? language) =>
        language is null ? null : ManagerFor(_themes.Current).GetDefinition(language.Highlighting);

    private HighlightingManager ManagerFor(ThemeKind theme)
    {
        if (!_managers.TryGetValue(theme, out var manager))
        {
            // Colors are read from resources at creation, so the manager is created for the current, applied theme.
            manager = HighlightingDefinitions.CreateManager(ReadPalette());
            _managers[theme] = manager;
        }

        return manager;
    }

    private Dictionary<string, Color> ReadPalette() =>
        SyntaxColorClassifier.PaletteKeys
            .Where(key => _application.TryFindResource(key) is SolidColorBrush)
            .ToDictionary(key => key, key => ((SolidColorBrush)_application.FindResource(key)).Color, StringComparer.Ordinal);
}
