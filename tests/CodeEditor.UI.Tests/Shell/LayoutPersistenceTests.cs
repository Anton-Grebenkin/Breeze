using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>
/// The layout survives a restart: an open bottom panel stays open, a maximized window stays within the screen.
/// </summary>
public sealed class LayoutPersistenceTests
{
    [Fact]
    public void OpenPanel_IsRestoredAfterRestart()
    {
        var userData = AppSession.CreateUserDataFolder();
        try
        {
            using (var first = AppSession.WithUserData(userData))
            {
                first.FocusWindow();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_U);
                first.WaitFor(AutomationIds.OutputText);
            }

            using var second = AppSession.WithUserData(userData);
            second.WaitFor(AutomationIds.OutputText);
        }
        finally
        {
            AppSession.DeleteQuietly(userData);
        }
    }

    [Fact]
    public void MaximizedAfterRestart_StaysInsideWorkArea()
    {
        var userData = AppSession.CreateUserDataFolder();
        try
        {
            using (var first = AppSession.WithUserData(userData))
            {
                first.Find(AutomationIds.CaptionMaximize).AsButton().Invoke();
                Retry.WhileFalse(() => first.MainWindow.Patterns.Window.Pattern.WindowVisualState.Value == WindowVisualState.Maximized, TimeSpan.FromSeconds(5));
            }

            // The window opens maximized: caption buttons and status bar stay on screen, not under the taskbar.
            using var second = AppSession.WithUserData(userData);
            var workArea = MonitorWorkArea.Of(second.MainWindow.Properties.NativeWindowHandle.Value);
            foreach (var id in new[] { AutomationIds.CaptionClose, AutomationIds.MenuBar, AutomationIds.StatusBarMessage })
            {
                var bounds = second.WaitFor(id).BoundingRectangle;
                Assert.True(workArea.Contains(bounds), $"{id} {bounds} выходит за рабочую область {workArea}.");
            }
        }
        finally
        {
            AppSession.DeleteQuietly(userData);
        }
    }
}
