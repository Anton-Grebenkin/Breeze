using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// The Terminal panel on a real shell (ADR 0045): <c>Ctrl+`</c> opens it in the folder and typed commands run; keys
/// that the shell needs (<c>Ctrl+W</c>) go to it, workbench keys (<c>Ctrl+Shift+P</c>) still work; the Terminal menu
/// opens another terminal and the button closes it.
/// </summary>
public sealed class TerminalTests : IDisposable
{
    private static readonly TimeSpan ShellTimeout = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan RetypeInterval = TimeSpan.FromSeconds(5);

    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void CtrlBacktick_OpensAShellInTheFolder_TypedCommandsRun()
    {
        using var session = AppSession.WithArguments(_folder);

        OpenTerminal(session);
        RunUntil(session, "echo breeze-terminal-ok > out.txt", () => File.Exists(Path.Combine(_folder, "out.txt")));

        Assert.Contains("breeze-terminal-ok", File.ReadAllText(Path.Combine(_folder, "out.txt")), StringComparison.Ordinal);
        session.SaveScreenshot("terminal-panel");
    }

    [Fact]
    public void FocusedTerminal_ShellKeysGoToTheShell_WorkbenchKeysStillWork()
    {
        File.WriteAllText(Path.Combine(_folder, "a.txt"), "текст");
        using var session = AppSession.WithArguments(_folder);
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
        session.WaitFor(AutomationIds.PaletteQuery);
        Keyboard.Type("a.txt");
        Keyboard.Type(VirtualKeyShort.ENTER);
        session.WaitFor("EditorTab.a.txt");

        OpenTerminal(session);
        RunUntil(session, "echo ready > ready.txt", () => File.Exists(Path.Combine(_folder, "ready.txt")));
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        Thread.Sleep(TimeSpan.FromSeconds(1));
        Assert.NotNull(session.TryFind("EditorTab.a.txt"));

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        session.WaitFor(AutomationIds.PaletteQuery);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
    }

    [Fact]
    public void TerminalMenu_OpensAnother_ButtonClosesIt()
    {
        using var session = AppSession.WithArguments(_folder);
        OpenTerminal(session);

        session.Find(AutomationIds.MenuItem("menubar.terminal")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem("terminal.new")).GuardedClick();
        session.WaitFor("Terminal.Session.2");

        session.Find("Terminal.Kill").GuardedClick();
        session.WaitUntilGone("Terminal.Session.2");
        Assert.NotNull(session.TryFind("Terminal.Session.1"));
    }

    private static void OpenTerminal(AppSession session)
    {
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_3);
        session.WaitFor("Terminal.Session.1");
        session.WaitFor("Terminal.Page");
    }

    // The shell may still be starting: the command is typed again until it has run.
    private static void RunUntil(AppSession session, string command, Func<bool> done)
    {
        var deadline = DateTime.UtcNow + ShellTimeout;
        while (!done())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Команда «{command}» не выполнилась в терминале: {session.SaveScreenshot("terminal-timeout")}");
            }

            Keyboard.Type(command);
            Keyboard.Type(VirtualKeyShort.ENTER);
            Retry.WhileFalse(done, RetypeInterval);
        }
    }
}
