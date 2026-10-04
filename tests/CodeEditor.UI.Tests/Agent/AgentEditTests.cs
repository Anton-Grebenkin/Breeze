using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// M2 acceptance on a fake model: the agent renames a method across files with a diff card, Apply, edits in tabs, and
/// a single <c>Ctrl+Z</c> undo per file.
/// </summary>
public sealed class AgentEditTests : IDisposable
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(10);

    private readonly FakeOpenAIServer _model = new();
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;
    private readonly string _a;
    private readonly string _b;

    public AgentEditTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        _a = Path.Combine(_folder, "A.cs");
        _b = Path.Combine(_folder, "B.cs");
        File.WriteAllText(_a, "class A\r\n{\r\n    public void Run() { }\r\n}\r\n");
        File.WriteAllText(_b, "class B\r\n{\r\n    void Go() => new A().Run();\r\n}\r\n");

        // The agent talks to a local fake model; the key is a test key that never leaves this server.
        // Require an approval card before edits: by default edits apply immediately and are reviewed in the editor.
        File.WriteAllText(Path.Combine(_userData, "settings.json"), $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false, "agent.approvals": "edits" }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
    }

    public void Dispose()
    {
        _model.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void RenameAcrossFiles_WithDiff_ApplyAndUndo()
    {
        _model
            .CallTool("read_file", new { path = "A.cs" })
            .CallTool("read_file", new { path = "B.cs" })
            .CallTool("apply_edits", new
            {
                edits = new[]
                {
                    new { path = "A.cs", oldText = "public void Run()", newText = "public void Execute()" },
                    new { path = "B.cs", oldText = "new A().Run()", newText = "new A().Execute()" },
                },
            })
            .Reply("Переименовал Run в Execute в двух файлах.");

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "Переименуй метод Run в Execute");
        Keyboard.Type(VirtualKeyShort.ENTER);

        session.WaitFor("Agent.Approval.Approve");
        session.SaveScreenshot("agent-approval");
        session.Find("Agent.Approval.Approve").GuardedClick();

        session.WaitForText("Agent.Approval.State", text => text == "Применено", FileTimeout);
        session.WaitFor("EditorTab.A.cs");
        session.WaitFor("EditorTab.B.cs");
        session.SaveScreenshot("agent-applied");

        // Saved right away (ADR 0040); one undo in the editor restores the file.
        WaitForFile(_a, text => text.Contains("public void Execute()", StringComparison.Ordinal));
        WaitForFile(_b, text => text.Contains("new A().Execute()", StringComparison.Ordinal));
        SaveTab(session, "A.cs");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        WaitForFile(_a, text => text == "class A\r\n{\r\n    public void Run() { }\r\n}\r\n");
        Assert.Equal(4, _model.Requests.Count);
    }

    private static void SaveTab(AppSession session, string title)
    {
        session.Find($"EditorTab.{title}").GuardedClick();
        session.WaitFor("TextEditor").GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
    }

    // The editor replaces the file while saving: a read at that moment fails and is retried.
    private static void WaitForFile(string path, Func<string, bool> predicate) =>
        Assert.True(Retry.WhileFalse(() => predicate(File.ReadAllText(path)), FileTimeout, ignoreException: true).Result, $"Файл {Path.GetFileName(path)}: {File.ReadAllText(path)}");
}
