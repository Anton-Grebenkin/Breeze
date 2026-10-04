using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>Command palette: opening by keyboard and mouse, filtering, running, closing.</summary>
public sealed class CommandPaletteTests(AppSession session) : IClassFixture<AppSession>
{
    private const string LightTheme = "workbench.theme.light";
    private const string DarkTheme = "workbench.theme.dark";

    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Keyboard_OpenFilterAndExecute()
    {
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        session.WaitForFocus(AutomationIds.PaletteQuery);

        session.TypeInto(AutomationIds.PaletteQuery, "светл");
        session.WaitFor(AutomationIds.PaletteItem(LightTheme));
        session.SaveScreenshot("palette-filtered");
        Keyboard.Type(VirtualKeyShort.ENTER);

        session.WaitUntilGone(AutomationIds.Palette);
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("светлая", StringComparison.Ordinal), UpdateTimeout);
    }

    [Fact]
    public void Mouse_OpenFromSearchBoxAndClickItem()
    {
        // The search box opens quick open; ">" switches to commands, as in VS Code.
        session.Find(AutomationIds.TitleBarSearch).GuardedClick();
        session.WaitForFocus(AutomationIds.PaletteQuery);
        session.TypeInto(AutomationIds.PaletteQuery, ">тём");

        session.WaitFor(AutomationIds.PaletteItem(DarkTheme)).GuardedClick();

        session.WaitUntilGone(AutomationIds.Palette);
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("тёмная", StringComparison.Ordinal), UpdateTimeout);
    }

    [Fact]
    public void F1_OpensAndEscapeCloses()
    {
        session.FocusWindow();
        Keyboard.Type(VirtualKeyShort.F1);
        session.WaitForFocus(AutomationIds.PaletteQuery);

        Keyboard.Type(VirtualKeyShort.ESCAPE);

        session.WaitUntilGone(AutomationIds.Palette);
    }

    [Fact]
    public void NoMatches_ShowsEmptyHint()
    {
        session.FocusWindow();
        Keyboard.Type(VirtualKeyShort.F1);
        session.WaitForFocus(AutomationIds.PaletteQuery);

        Keyboard.Type("qqqq");

        session.WaitFor(AutomationIds.PaletteEmpty);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone(AutomationIds.Palette);
    }
}
