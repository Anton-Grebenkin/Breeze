using System.Diagnostics;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using FlaApplication = FlaUI.Core.Application;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// Restart (needed for language changes, ADR 0011): the window closes the usual way and a new process reopens the same
/// folder from the saved session.
/// </summary>
public sealed class RestartTests : IDisposable
{
    private const string RestartCommand = "workbench.restart";
    private const string ProcessName = "Breeze";

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RelaunchTimeout = TimeSpan.FromSeconds(30);

    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;

    public RestartTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "A.cs"), "class A { }\r\n");
    }

    // Where the exit got stuck: the last lines of the old process's log.
    private string LogTail() => string.Join("\n", AppSession.ReadLog(_userData).Split('\n').TakeLast(25));

    public void Dispose()
    {
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Restart_ClosesWindow_NewProcessReopensFolder()
    {
        var started = DateTime.Now;
        int oldProcess;
        using (var session = AppSession.WithUserData(_userData, _folder))
        {
            session.WaitFor("Explorer.Node.A.cs");
            oldProcess = session.ProcessId;
            session.FocusWindow();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            session.WaitForFocus(AutomationIds.PaletteQuery);
            session.TypeInto(AutomationIds.PaletteQuery, "перезап");
            // Click the command itself: Enter would run the first row, and Docker commands match the query too.
            session.WaitFor(AutomationIds.PaletteItem(RestartCommand)).GuardedClick();

            Assert.True(Retry.WhileFalse(() => HasExited(oldProcess), ExitTimeout).Result, "Старое окно не закрылось. Конец журнала:\n" + LogTail());
        }

        using var relaunched = Retry.WhileNull(() => FindRelaunched(started, oldProcess), RelaunchTimeout).Result
            ?? throw new InvalidOperationException("Новый процесс не запустился.");
        using var application = FlaApplication.Attach(relaunched);
        using var automation = new UIA3Automation();
        try
        {
            var window = application.GetMainWindow(automation, RelaunchTimeout)
                ?? throw new InvalidOperationException("У нового процесса нет главного окна.");
            var node = Retry.WhileNull(() => window.FindFirstDescendant(condition => condition.ByAutomationId("Explorer.Node.A.cs")), ExitTimeout).Result;
            Assert.NotNull(node);
        }
        finally
        {
            application.Close();
            if (!Retry.WhileFalse(() => application.HasExited, ExitTimeout).Result)
            {
                application.Kill();
            }
        }
    }

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

    // Only the test build's process started after the test began: the user's own running editor is left alone.
    private static Process? FindRelaunched(DateTime started, int oldProcess)
    {
        var path = Path.GetFullPath(AppSession.AppExePath());
        Process? found = null;
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            if (found is null && process.Id != oldProcess && IsSameExecutable(process, path) && process.StartTime >= started)
            {
                found = process;
                continue;
            }

            process.Dispose();
        }

        return found;
    }

    private static bool IsSameExecutable(Process process, string path)
    {
        try
        {
            return string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
