using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// Settings: <c>settings.json</c> values apply at startup and on file edits, <c>Ctrl+,</c> opens the file, and the
/// theme command writes the choice to it.
/// </summary>
public sealed class SettingsTests : IDisposable
{
    private const string Indentation = "StatusBar.editor.indentation";

    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _settingsFile;

    public SettingsTests()
    {
        _settingsFile = Path.Combine(_userData, "settings.json");
        File.WriteAllText(_settingsFile, "// тест\n{\n    \"editor.tabSize\": 2,\n    \"workbench.colorTheme\": \"Light+\"\n}\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(_userData);

    [Fact]
    public void SettingsFile_AppliesAtStartAndLive_ThemeChoiceIsSaved()
    {
        using var session = AppSession.WithUserData(_userData);
        session.FocusWindow();

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA);
        session.WaitFor("EditorTab.settings.json");
        session.WaitForText(Indentation, text => text == "Пробелы: 2", UpdateTimeout);
        session.SaveScreenshot("settings-light");

        // The theme command writes the choice to the file and keeps the comment.
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
        var saved = Retry.WhileFalse(() => File.ReadAllText(_settingsFile).Contains("\"workbench.colorTheme\": \"Dark+\"", StringComparison.Ordinal), UpdateTimeout).Result;
        Assert.True(saved, $"Тема не записана. Строка состояния: {session.Find(AutomationIds.StatusBarMessage).Name}");
        Assert.StartsWith("// тест", File.ReadAllText(_settingsFile), StringComparison.Ordinal);

        // Editing the file on disk applies without a restart.
        File.WriteAllText(_settingsFile, "{ \"editor.tabSize\": 8, \"editor.insertSpaces\": false }");
        session.WaitForText(Indentation, text => text == "Табуляция: 8", UpdateTimeout);
    }
}
