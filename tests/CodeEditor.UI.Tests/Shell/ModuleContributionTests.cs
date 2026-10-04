using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>
/// M0 done criterion: the Output module adds a tool window, a palette command and a menu item without shell changes.
/// </summary>
public sealed class ModuleContributionTests(AppSession session) : IClassFixture<AppSession>
{
    private const string ShowOutputCommand = "workbench.view.output";

    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Shortcut_ShowsOutputPanelWithApplicationLog()
    {
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_U);

        session.WaitFor(AutomationIds.OutputText);
        session.WaitForValue(AutomationIds.OutputText, text => text.Contains("Window rendered", StringComparison.Ordinal), UpdateTimeout);
        session.SaveScreenshot("output-panel");

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_J);
        session.WaitUntilGone(AutomationIds.OutputText);
    }

    [Fact]
    public void ViewMenu_ContainsOutput()
    {
        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();

        session.WaitFor(AutomationIds.MenuItem(ShowOutputCommand)).GuardedClick();

        session.WaitFor(AutomationIds.OutputText);
        session.Find(AutomationIds.PanelClose).GuardedClick();
        session.WaitUntilGone(AutomationIds.OutputText);
    }

    [Fact]
    public void Palette_ContainsShowOutputCommand()
    {
        session.FocusWindow();
        Keyboard.Type(VirtualKeyShort.F1);
        session.WaitForFocus(AutomationIds.PaletteQuery);

        session.TypeInto(AutomationIds.PaletteQuery, "вывод");

        session.WaitFor(AutomationIds.PaletteItem(ShowOutputCommand));
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone(AutomationIds.Palette);
    }
}
