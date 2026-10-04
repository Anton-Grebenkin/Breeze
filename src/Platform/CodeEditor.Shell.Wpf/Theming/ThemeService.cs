using System.Windows;
using CodeEditor.Shell.Theming;
using CodeEditor.UI.Themes;

namespace CodeEditor.Shell.Wpf.Theming;

/// <summary>
/// Switches the theme by swapping the color dictionary in application resources; all <c>DynamicResource</c>s update
/// themselves. Dictionaries are cached so switching back doesn't parse XAML again.
/// </summary>
public sealed class ThemeService(Application application) : IThemeService
{
    private readonly Dictionary<ThemeKind, ResourceDictionary> _cache = [];
    private ResourceDictionary? _applied;

    public ThemeKind Current { get; private set; } = ThemeKind.Dark;

    public event EventHandler? Changed;

    public void Apply(ThemeKind theme)
    {
        if (_applied is not null && theme == Current)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        var colors = GetColors(theme);

        // Colors go first: styles in the other dictionaries reference them via DynamicResource.
        if (_applied is null)
        {
            dictionaries.Insert(0, colors);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(_applied)] = colors;
        }

        _applied = colors;
        Current = theme;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private ResourceDictionary GetColors(ThemeKind theme)
    {
        if (!_cache.TryGetValue(theme, out var colors))
        {
            colors = new ResourceDictionary { Source = SourceOf(theme) };
            _cache[theme] = colors;
        }

        return colors;
    }

    private static Uri SourceOf(ThemeKind theme) => theme switch
    {
        ThemeKind.Dark => ThemeDictionaries.DarkColors,
        ThemeKind.Light => ThemeDictionaries.LightColors,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };
}
