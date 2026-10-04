using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>Keyboard and theme: the Ctrl+K Ctrl+T chord goes through KeyboardRouter and switches the theme.</summary>
public sealed class ThemeAndKeyboardTests(AppSession session) : IClassFixture<AppSession>
{
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void ToggleThemeChord_SwitchesThemeBackAndForth()
    {
        PressToggleTheme();
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("светлая", StringComparison.Ordinal), UpdateTimeout);
        session.SaveScreenshot("theme-light");

        PressToggleTheme();
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("тёмная", StringComparison.Ordinal), UpdateTimeout);
        session.SaveScreenshot("theme-dark");
    }

    [Fact]
    public void FirstChordOfSequence_ShowsWaitingHint()
    {
        session.MainWindow.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);

        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("Ожидание", StringComparison.Ordinal), UpdateTimeout);

        Keyboard.Type(VirtualKeyShort.ESCAPE);
    }

    private void PressToggleTheme()
    {
        session.MainWindow.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
    }
}
