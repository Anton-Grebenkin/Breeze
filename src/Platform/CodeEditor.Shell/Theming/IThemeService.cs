namespace CodeEditor.Shell.Theming;

/// <summary>
/// The current color theme. The implementation (Shell.Wpf) swaps the color dictionary in application resources.
/// </summary>
public interface IThemeService
{
    ThemeKind Current { get; }

    event EventHandler? Changed;

    void Apply(ThemeKind theme);
}
