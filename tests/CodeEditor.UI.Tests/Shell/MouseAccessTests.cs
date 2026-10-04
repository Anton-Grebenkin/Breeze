using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>Everything available from the keyboard is available by mouse too: menu bar, Manage, welcome page.</summary>
public sealed class MouseAccessTests(AppSession session) : IClassFixture<AppSession>
{
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void MenuBar_ViewThemeLight_AppliesTheme()
    {
        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem("menubar.view.theme")).GuardedClick();
        var light = session.WaitFor(AutomationIds.MenuItem("workbench.theme.light"));
        session.SaveScreenshot("menu-view-theme");

        light.GuardedClick();

        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("светлая", StringComparison.Ordinal), UpdateTimeout);
        session.WaitUntilGone(AutomationIds.MenuItem("workbench.theme.light"));
    }

    [Fact]
    public void MenuBar_FileMenu_HasExitWithShortcut()
    {
        session.Find(AutomationIds.MenuItem("menubar.file")).GuardedClick();

        var exit = session.WaitFor(AutomationIds.MenuItem("workbench.quit"));
        Assert.Contains("Выход", exit.Name, StringComparison.Ordinal);

        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone(AutomationIds.MenuItem("workbench.quit"));
    }

    [Fact]
    public void ManageButton_OpensMenuWithPalette()
    {
        session.Find(AutomationIds.ActivityBarManage).GuardedClick();
        session.SaveScreenshot("manage-menu");

        session.WaitFor(AutomationIds.MenuItem("workbench.showCommands")).GuardedClick();

        session.WaitForFocus(AutomationIds.PaletteQuery);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone(AutomationIds.Palette);
    }

    [Fact]
    public void WelcomeShortcut_Click_ExecutesCommand()
    {
        var before = session.Find(AutomationIds.StatusBarMessage).Name;

        session.Find(AutomationIds.WelcomeItem("workbench.theme.toggle")).GuardedClick();

        session.WaitForText(AutomationIds.StatusBarMessage, text => text != before && text.StartsWith("Тема:", StringComparison.Ordinal), UpdateTimeout);
    }

    [Fact]
    public void TitleBarLayoutToggles_ShowAndHideAreas()
    {
        // The right area with the agent: a title bar button opens and closes it.
        session.Find("TitleBar.ToggleSecondarySideBar").GuardedClick();
        session.WaitFor("Agent.Input");
        session.SaveScreenshot("layout-secondary-open");
        session.Find("TitleBar.ToggleSecondarySideBar").GuardedClick();
        session.WaitUntilGone("Agent.Input");

        session.Find("TitleBar.TogglePanel").GuardedClick();
        session.WaitFor(AutomationIds.OutputText);
        session.Find("TitleBar.TogglePanel").GuardedClick();
        session.WaitUntilGone(AutomationIds.OutputText);
    }
}
