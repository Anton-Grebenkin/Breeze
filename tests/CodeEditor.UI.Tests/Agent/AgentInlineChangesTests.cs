using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// Agent edits in the editor (ADR 0014): after an edit the tab shows the agent changes bar with buttons per hunk;
/// Accept saves the file, Reject restores the text.
/// </summary>
public sealed class AgentInlineChangesTests : IDisposable
{
    private readonly FakeOpenAIServer _model = new();
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;
    private readonly string _file;

    public AgentInlineChangesTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        _file = Path.Combine(_folder, "A.cs");
        File.WriteAllText(_file, "class A\r\n{\r\n    void Run() { }\r\n}\r\n");
        // Edits apply immediately (the default) and are reviewed in the editor; no project here, so no build is needed.
        File.WriteAllText(Path.Combine(_userData, "settings.json"), $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
        _model
            .CallTool("read_file", new { path = "A.cs" })
            .CallTool("apply_edits", new { edits = new[] { new { path = "A.cs", oldText = "    void Run() { }", newText = "    void Run() { }\r\n    void Stop() { }" } } })
            .Reply("Добавил Stop.");
    }

    public void Dispose()
    {
        _model.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Edit_ShowsChangesBar_AcceptSavesFile()
    {
        using var session = Start();

        session.SaveScreenshot("agent-inline-changes");
        session.Find("TextEditor.Hunk.Accept").GuardedClick();

        session.WaitUntilGone("TextEditor.AgentChanges");
        Assert.True(Retry.WhileFalse(() => File.ReadAllText(_file).Contains("void Stop()", StringComparison.Ordinal), TimeSpan.FromSeconds(10)).Result, File.ReadAllText(_file));
    }

    [Fact]
    public void Edit_RejectRestoresText_FileUntouched()
    {
        using var session = Start();

        session.Find("TextEditor.Hunk.Reject").GuardedClick();

        session.WaitUntilGone("TextEditor.AgentChanges");
        session.SaveScreenshot("agent-inline-rejected");
        Assert.DoesNotContain("void Stop()", File.ReadAllText(_file), StringComparison.Ordinal);
    }

    private AppSession Start()
    {
        var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "Добавь метод Stop");
        Keyboard.Type(VirtualKeyShort.ENTER);

        try
        {
            session.WaitFor("TextEditor.AgentChanges.Summary");
            session.WaitForText("TextEditor.AgentChanges.Summary", text => text == "Правки агента: 1 · +1 −0", TimeSpan.FromSeconds(15));
        }
        catch (Exception exception)
        {
            var screenshot = session.SaveScreenshot("agent-inline-timeout");
            throw new InvalidOperationException($"Полоса правок не появилась. Снимок: {screenshot}. Журнал:\n{AppSession.ReadLog(_userData)}", exception);
        }

        session.WaitFor("TextEditor.Hunk.Accept");
        return session;
    }
}
