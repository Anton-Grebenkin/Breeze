using System.Diagnostics;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// Several windows, one process each (ADR 0044): a launch with a folder or file that a window already has hands it to
/// that window and exits; "New Window" starts another process.
/// </summary>
public sealed class WindowsTests : IDisposable
{
    private static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(15);

    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;

    private readonly DateTime _testStarted = DateTime.Now;

    public void Dispose()
    {
        // Only windows this test started: a user's Breeze is never touched.
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppSession.AppExePath())))
        {
            using (process)
            {
                if (IsOurs(process))
                {
                    process.Kill();
                }
            }
        }

        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void SecondLaunch_SameFolder_GoesToTheOpenWindow()
    {
        using var session = AppSession.WithArguments(_folder);
        WaitUntilListening(session);

        using var second = AppSession.Launch(session.UserDataFolder, _folder);

        Assert.True(second.WaitForExit(HandOverTimeout), "Второй запуск не завершился.");
        Assert.Equal(0, second.ExitCode);
        Retry.WhileFalse(() => AppSession.ReadLog(session.UserDataFolder).Contains("Request from another launch: " + _folder, StringComparison.Ordinal), HandOverTimeout);
    }

    [Fact]
    public void SecondLaunch_File_OpensInTheOpenWindow()
    {
        var file = Path.Combine(_folder, "notes.md");
        File.WriteAllText(file, "# Заметки");
        using var session = AppSession.WithArguments(_folder);
        WaitUntilListening(session);

        using var second = AppSession.Launch(session.UserDataFolder, file);

        Assert.True(second.WaitForExit(HandOverTimeout), "Второй запуск не завершился.");
        session.WaitFor("EditorTab.notes.md");
    }

    // "Open in Breeze" on a file while no window is open: the file alone, as in VS Code.
    [Fact]
    public void FileArgument_NoWindows_OpensTheFileWithoutFolder()
    {
        var file = Path.Combine(_folder, "todo.txt");
        File.WriteAllText(file, "купить молоко");

        using var session = AppSession.WithArguments(file);

        session.WaitFor("EditorTab.todo.txt");
        Assert.Equal("Breeze", session.MainWindow.Title);
    }

    [Fact]
    public void NewWindow_StartsAnotherProcess()
    {
        using var session = AppSession.WithArguments(_folder);
        WaitUntilListening(session);

        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_N);

        var window = Retry.WhileNull(() => OtherWindow(session.ProcessId), HandOverTimeout).Result;
        Assert.NotNull(window);
    }

    // The window joins the registry after its first frame; a launch before that opens a window of its own.
    private static void WaitUntilListening(AppSession session) =>
        Retry.WhileFalse(() => File.Exists(Path.Combine(session.UserDataFolder, "windows", $"{session.ProcessId}.json")), HandOverTimeout, throwOnTimeout: true);

    private Process? OtherWindow(int sessionProcessId) =>
        Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppSession.AppExePath()))
            .FirstOrDefault(process => process.Id != sessionProcessId && IsOurs(process) && process.MainWindowHandle != 0);

    private bool IsOurs(Process process)
    {
        try
        {
            return string.Equals(process.MainModule?.FileName, AppSession.AppExePath(), StringComparison.OrdinalIgnoreCase) && process.StartTime >= _testStarted;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
