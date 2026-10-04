using System.Diagnostics;
using System.Text;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// Git panel on real git in a temp repository (ADR 0032): <c>Ctrl+Shift+G</c> focuses the message box, a new file is
/// staged from the context menu, <c>Ctrl+Enter</c> commits the index, and clicking a changed file opens its diff in an
/// editor tab.
/// </summary>
public sealed class GitPanelTests : IDisposable
{
    private const string Message = "Add New.cs";

    private readonly string _folder;

    public GitPanelTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "repo")).FullName;
        Git("init", "--initial-branch=main");
        Git("config", "user.name", "UI Test");
        Git("config", "user.email", "ui-test@example.invalid");
        Git("config", "commit.gpgsign", "false");
        File.WriteAllText(Path.Combine(_folder, "Program.cs"), "class Program { }\n");
        Git("add", "Program.cs");
        Git("commit", "-m", "Start");
        File.WriteAllText(Path.Combine(_folder, "Program.cs"), "class Program { static void Main() { } }\n");
        File.WriteAllText(Path.Combine(_folder, "New.cs"), "class New { }\n");
    }

    // Git objects are read-only; the folder can't be deleted without clearing the attribute.
    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void StageCommitAndOpenChanges()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Program.cs");

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_G);
        session.WaitForFocus("Git.CommitMessage");

        session.WaitFor("Git.Change.Changes.New.cs").GuardedRightClick();
        session.WaitFor(AutomationIds.MenuItem("git.stage")).GuardedClick();
        session.WaitFor("Git.Change.Staged.New.cs");

        session.Find("Git.CommitMessage").GuardedClick();
        session.WaitForFocus("Git.CommitMessage");
        session.TypeInto("Git.CommitMessage", Message);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.RETURN);
        session.WaitUntilGone("Git.Change.Staged.New.cs");
        Assert.Equal(Message, Git("log", "-1", "--format=%s").Trim());

        session.Find("Git.Change.Changes.Program.cs").GuardedClick();
        session.WaitFor("EditorTab.Program.cs (изменения)");
        session.SaveScreenshot("git-panel");
    }

    // Config is set for this repository only; the user's global git config is untouched.
    private string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = _folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {error}");
        return output;
    }
}
