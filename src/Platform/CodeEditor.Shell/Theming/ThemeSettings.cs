using CodeEditor.Shell.Settings;
using Microsoft.Extensions.Options;

namespace CodeEditor.Shell.Theming;

/// <summary>
/// Applies the theme from settings before the window is created, and again whenever <c>workbench.colorTheme</c>
/// changes (manual edit of <c>settings.json</c> or a theme command).
/// </summary>
public sealed class ThemeSettings(IThemeService themes, IOptionsMonitor<WorkbenchOptions> options) : IDisposable
{
    private IDisposable? _subscription;

    public void Start()
    {
        themes.Apply(ThemeNames.ToKind(options.CurrentValue.ColorTheme));
        _subscription ??= options.OnChange(changed => themes.Apply(ThemeNames.ToKind(changed.ColorTheme)));
    }

    public void Dispose() => _subscription?.Dispose();
}
