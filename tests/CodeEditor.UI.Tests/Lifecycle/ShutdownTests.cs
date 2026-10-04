using System.Diagnostics;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// Closing the window with no unsaved files exits immediately without prompts. Regression: a second Close() inside the
/// Closing handler silently didn't close the window.
/// </summary>
public sealed class ShutdownTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void CloseButton_ExitsApplication()
    {
        using var session = AppSession.WithArguments();

        session.Find(AutomationIds.CaptionClose).GuardedClick();

        AssertExited(session.ProcessId);
    }

    [Fact]
    public void FileExit_ExitsApplication()
    {
        using var session = AppSession.WithArguments();

        session.Find(AutomationIds.MenuItem("menubar.file")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem("workbench.quit")).GuardedClick();

        AssertExited(session.ProcessId);
    }

    private static void AssertExited(int processId) =>
        Assert.True(Retry.WhileFalse(() => HasExited(processId), ExitTimeout).Result, "Приложение не завершилось.");

    private static bool HasExited(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
