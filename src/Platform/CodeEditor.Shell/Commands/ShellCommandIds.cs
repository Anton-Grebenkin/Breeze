namespace CodeEditor.Shell.Commands;

/// <summary>Shell command ids.</summary>
public static class ShellCommandIds
{
    public const string ShowCommands = "workbench.showCommands";

    /// <summary>Quick open for files (<c>Ctrl+P</c>).</summary>
    public const string QuickOpen = "workbench.quickOpen";

    /// <summary>Palette in ":" mode, go to line (<c>Ctrl+G</c>).</summary>
    public const string GoToLine = "workbench.goToLine";

    /// <summary>
    /// Navigates in the active editor; the argument is a 1-based line number or an <see cref="Editors.EditorLocation"/>.
    /// Registered by the editor module.
    /// </summary>
    public const string EditorGoToLine = "editor.goToLine";

    /// <summary>Focuses the text of the active editor. Registered by the editor module.</summary>
    public const string FocusActiveEditor = "workbench.focusActiveEditor";
    public const string ToggleTheme = "workbench.theme.toggle";
    public const string DarkTheme = "workbench.theme.dark";
    public const string LightTheme = "workbench.theme.light";
    public const string LanguageAuto = "workbench.language.auto";
    public const string LanguageRussian = "workbench.language.ru";
    public const string LanguageEnglish = "workbench.language.en";

    /// <summary>Closes the window as on exit and starts the app again. Registered by Shell.Wpf.</summary>
    public const string Restart = "workbench.restart";

    /// <summary>Opens a file in the editor; the argument is a full path. Registered by the editor module.</summary>
    public const string OpenFile = "workbench.file.open";
}
