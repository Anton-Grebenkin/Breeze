using CodeEditor.UI.Tests.Infrastructure;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>Session restore: after a restart without arguments the previous folder opens with its tabs.</summary>
public sealed class SessionTests : IDisposable
{
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;

    public SessionTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "Program.cs"), "class Program { }\r\n");
        File.WriteAllText(Path.Combine(_folder, "Notes.txt"), "заметки\r\n");
    }

    public void Dispose()
    {
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Restart_RestoresFolderAndTabs()
    {
        using (var first = AppSession.WithUserData(_userData, _folder))
        {
            first.WaitFor("Explorer.Node.Program.cs").GuardedDoubleClick();
            first.WaitFor("EditorTab.Program.cs");
            first.WaitFor("Explorer.Node.Notes.txt").GuardedDoubleClick();
            first.WaitFor("EditorTab.Notes.txt");
        }

        using var second = AppSession.WithUserData(_userData);

        second.WaitFor("Explorer.Node.Program.cs");
        second.WaitFor("EditorTab.Program.cs");
        second.WaitFor("EditorTab.Notes.txt");
        second.SaveScreenshot("session-restored");
    }
}
