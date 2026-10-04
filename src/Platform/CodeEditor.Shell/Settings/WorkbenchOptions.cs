namespace CodeEditor.Shell.Settings;

/// <summary>The <c>workbench</c> settings section, e.g. <c>"workbench.colorTheme": "Light+"</c>.</summary>
public sealed class WorkbenchOptions
{
    public const string Section = "workbench";

    public string ColorTheme { get; set; } = Theming.ThemeNames.Dark;
}
