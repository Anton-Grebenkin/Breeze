namespace CodeEditor.Shell.Theming;

/// <summary>Theme names in <c>settings.json</c>, matching VS Code's built-in themes.</summary>
public static class ThemeNames
{
    public const string Dark = "Dark+";
    public const string Light = "Light+";

    public static string ToName(ThemeKind theme) => theme == ThemeKind.Light ? Light : Dark;

    /// <summary>Unknown names map to the default dark theme.</summary>
    public static ThemeKind ToKind(string? name) =>
        string.Equals(name, Light, StringComparison.OrdinalIgnoreCase) ? ThemeKind.Light : ThemeKind.Dark;
}
