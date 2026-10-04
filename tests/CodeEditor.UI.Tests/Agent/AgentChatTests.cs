using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// Agent panel on a fake model: Markdown reply, the open file as context, the context ring, the model parameters menu,
/// a new chat and returning to the previous one from recents.
/// </summary>
public sealed class AgentChatTests : IDisposable
{
    private const string Answer = """
        ## Что делает `Run`

        Метод **запускает** работу. Смотрите [A.cs:3](A.cs:3).

        - первый пункт
        - второй пункт

        ```csharp
        public void Run() { Console.WriteLine("hi"); }
        ```
        """;

    private readonly FakeOpenAIServer _model = new();
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;

    public AgentChatTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "A.cs"), "class A\r\n{\r\n    public void Run() { }\r\n}\r\n");

        // The agent talks to a local fake model; the key is a test key that never leaves this server.
        File.WriteAllText(Path.Combine(_userData, "settings.json"),
            $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false, "agent.contextWindow": 10000 }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
    }

    public void Dispose()
    {
        _model.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void MarkdownAnswer_Context_Parameters_AndRecentChats()
    {
        _model.ReplyWithUsage(Answer, promptTokens: 2_000, completionTokens: 500);

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs").GuardedDoubleClick();
        session.WaitFor("EditorTab.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.WaitFor("Agent.Welcome");
        Assert.Equal("A.cs", session.WaitFor("Agent.ActiveFile").Name);

        session.TypeInto("Agent.Input", "Что делает Run?");
        Keyboard.Type(VirtualKeyShort.ENTER);
        session.WaitUntilGone("Agent.Welcome");
        session.WaitForText("Agent.Context", text => text.Contains("25%", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        session.SaveScreenshot("agent-markdown");
        Assert.Contains("Открыт файл A.cs, курсор в строке", _model.Requests.Single(), StringComparison.Ordinal);

        session.Find("Agent.Context").GuardedClick();
        session.WaitFor("Agent.ContextDetails");
        session.SaveScreenshot("agent-context");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone("Agent.ContextDetails");

        session.Find("Agent.Parameters").GuardedClick();
        session.WaitFor("Agent.Parameters.agent.reasoningEffort").GuardedClick();
        var medium = session.WaitFor("Agent.Parameters.agent.reasoningEffort.3");
        session.SaveScreenshot("agent-parameters");
        medium.GuardedClick();
        session.WaitUntilGone("Agent.Parameters.agent.reasoningEffort");
        session.WaitForText(AutomationIds.StatusBarMessage, text => text == "Рассуждения: средний", TimeSpan.FromSeconds(5));

        // The menu is rebuilt after the choice: it opens again with the check on the new value.
        session.Find("Agent.Parameters").GuardedClick();
        session.WaitFor("Agent.Parameters.agent.reasoningEffort").GuardedClick();
        session.WaitFor("Agent.Parameters.agent.reasoningEffort.3");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone("Agent.Parameters.agent.reasoningEffort");

        session.Find("Agent.More").GuardedClick();
        session.WaitFor("Agent.More.DeleteChat");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone("Agent.More.DeleteChat");

        session.Find("Agent.NewChat").GuardedClick();
        session.WaitFor("Agent.Welcome");
        session.WaitFor("Agent.RecentChat");
        session.SaveScreenshot("agent-welcome");
        session.Find("Agent.RecentChat").GuardedClick();
        session.WaitUntilGone("Agent.Welcome");
        session.WaitForText("Agent.Title", text => text == "Что делает Run?", TimeSpan.FromSeconds(5));

        // The log has the model request with tokens and the agent turn, but not the question or answer text.
        var log = AppSession.ReadLog(_userData);
        Assert.Contains("AgentConversation: Model test/model: ", log, StringComparison.Ordinal);
        Assert.Contains("tokens: input 2000, output 500", log, StringComparison.Ordinal);
        Assert.Contains("AgentActivityLog: Agent turn finished", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Что делает Run", log, StringComparison.Ordinal);
    }
}
