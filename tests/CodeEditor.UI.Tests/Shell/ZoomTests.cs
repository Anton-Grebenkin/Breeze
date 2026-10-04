using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>
/// Interface zoom with VS Code keys: <c>Ctrl+=</c> scales the whole window (the search box in the title bar grows), the
/// level goes to <c>settings.json</c> and the status bar; <c>Ctrl+0</c> returns to 100 % and removes the key.
/// </summary>
public sealed class ZoomTests(AppSession session) : IClassFixture<AppSession>
{
    private const string SettingKey = "\"window.zoom\": 110";

    // 110 % of the height, with a margin for rounding to device pixels.
    private const double ZoomedRatio = 1.05;

    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void CtrlEquals_ScalesWindow_CtrlZero_ResetsIt()
    {
        var normal = SearchBoxHeight();

        Press(VirtualKeyShort.OEM_PLUS);
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("110", StringComparison.Ordinal), UpdateTimeout);
        Retry.WhileFalse(() => SearchBoxHeight() > normal * ZoomedRatio, UpdateTimeout, throwOnTimeout: true);
        Assert.Contains(SettingKey, File.ReadAllText(SettingsFile()), StringComparison.Ordinal);
        session.SaveScreenshot("zoom-110");

        Press(VirtualKeyShort.KEY_0);
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("100", StringComparison.Ordinal), UpdateTimeout);
        Retry.WhileFalse(() => Math.Abs(SearchBoxHeight() - normal) < 1, UpdateTimeout, throwOnTimeout: true);
        Assert.DoesNotContain("window.zoom", File.ReadAllText(SettingsFile()), StringComparison.Ordinal);
    }

    private double SearchBoxHeight() => session.Find(AutomationIds.TitleBarSearch).BoundingRectangle.Height;

    private string SettingsFile() => Path.Combine(session.UserDataFolder, "settings.json");

    private void Press(VirtualKeyShort key)
    {
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, key);
    }
}
