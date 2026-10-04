namespace CodeEditor.UI.Themes;

/// <summary>
/// Design system dictionary URIs. The color dictionary is swapped on theme change; the others are fixed.
/// </summary>
public static class ThemeDictionaries
{
    private const string Root = "pack://application:,,,/CodeEditor.UI;component/Themes/";

    public static Uri DarkColors { get; } = new(Root + "Colors.Dark.xaml");

    public static Uri LightColors { get; } = new(Root + "Colors.Light.xaml");
}
