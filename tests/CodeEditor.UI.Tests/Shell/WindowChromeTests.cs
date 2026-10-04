using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>Custom window chrome: title bar, search box, caption buttons.</summary>
public sealed class WindowChromeTests(AppSession session) : IClassFixture<AppSession>
{
    private static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(AutomationIds.TitleBarSearch)]
    [InlineData(AutomationIds.MenuBar)]
    [InlineData(AutomationIds.ActivityBarManage)]
    [InlineData(AutomationIds.CaptionMinimize)]
    [InlineData(AutomationIds.CaptionMaximize)]
    [InlineData(AutomationIds.CaptionClose)]
    [InlineData(AutomationIds.Welcome)]
    public void Chrome_HasElement(string automationId)
    {
        Assert.NotNull(session.Find(automationId));
    }

    [Fact]
    public void MaximizeButton_TogglesWindowState()
    {
        var window = session.MainWindow.Patterns.Window.Pattern;

        session.Find(AutomationIds.CaptionMaximize).AsButton().Invoke();
        Retry.WhileFalse(() => window.WindowVisualState.Value == WindowVisualState.Maximized, StateTimeout, throwOnTimeout: true);

        session.Find(AutomationIds.CaptionMaximize).AsButton().Invoke();
        Retry.WhileFalse(() => window.WindowVisualState.Value == WindowVisualState.Normal, StateTimeout, throwOnTimeout: true);
    }
}
