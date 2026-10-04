using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// Agent workflow on a fake model: a task plan above the input and a question to the user with options; the turn waits
/// for the answer and continues.
/// </summary>
public sealed class AgentWorkflowTests : IDisposable
{
    private readonly FakeOpenAIServer _model = new();
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;

    public AgentWorkflowTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "A.cs"), "class A { }\r\n");

        // The agent talks to a local fake model; the key is a test key that never leaves this server. Auto memory is
        // off: its model request would throw off the request counts in these scenarios.
        File.WriteAllText(Path.Combine(_userData, "settings.json"), $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
    }

    public void Dispose()
    {
        _model.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Plan_AndQuestion_AreShown_AndAnswered()
    {
        _model
            .CallTool("manage_todo", new
            {
                items = new[]
                {
                    new { id = "1", title = "Найти логгер", status = "completed" },
                    new { id = "2", title = "Выбрать вариант", status = "in_progress" },
                    new { id = "3", title = "Добавить запись", status = "pending" },
                },
            })
            .CallTool("ask_user", new { question = "Какой логгер использовать?", options = new[] { "Встроенный", "Serilog" } })
            .Reply("Беру встроенный логгер.")
            .Reply("Логирование добавлено.");

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "Добавь логирование");
        Keyboard.Type(VirtualKeyShort.ENTER);

        session.WaitFor("Agent.Todo");
        session.WaitForText("Agent.Todo.Toggle", text => text == "План · 1 из 3", TimeSpan.FromSeconds(10));
        var option = session.WaitFor("Agent.Question.Option");
        session.SaveScreenshot("agent-plan-question");
        option.GuardedClick();

        session.WaitUntilGone("Agent.Question.Option");
        // The turn ended with text while plan items were open, so one reminder to continue follows (ADR 0012).
        Assert.True(Retry.WhileFalse(() => _model.Requests.Count == 4, TimeSpan.FromSeconds(10)).Result, $"Запросов к модели: {_model.Requests.Count}");
        var requests = _model.Requests.ToArray();
        Assert.Contains("Пользователь ответил: Встроенный", requests[2], StringComparison.Ordinal);
        Assert.Contains("В плане остались открытые пункты", requests[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Turn_ShowsActivityBlock_CollapsedAfterAnswer_ExpandsOnClick()
    {
        _model
            .SayAndCallTool("Смотрю структуру папки.", "list_dir", new { path = "." })
            .SayAndCallTool("Читаю единственный файл.", "read_file", new { path = "A.cs" })
            .Reply("## Что это\n\nОдин класс `A` без членов: `A.cs:1`.\n\n| Файл | Роль |\n| --- | --- |\n| `A.cs` | пустой класс |");

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "изучи проект");
        Keyboard.Type(VirtualKeyShort.ENTER);

        // The activity block is expanded during the turn and collapses to a summary header after the answer.
        session.WaitFor("Agent.Activity");
        Assert.True(Retry.WhileFalse(() => _model.Requests.Count == 3, TimeSpan.FromSeconds(10)).Result, $"Запросов к модели: {_model.Requests.Count}");
        var header = session.WaitForText("Agent.Activity", text => text.StartsWith("Работал", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        Assert.EndsWith("2 действия", header, StringComparison.Ordinal);
        session.WaitUntilGone("Agent.Progress");
        session.SaveScreenshot("agent-activity-collapsed");

        session.Find("Agent.Activity").GuardedClick();

        Assert.True(Retry.WhileFalse(() => Count(session, "Agent.Progress") == 2 && Count(session, "Agent.Tool") == 2, TimeSpan.FromSeconds(5)).Result);
        session.SaveScreenshot("agent-activity");
    }

    private static int Count(AppSession session, string automationId) =>
        session.MainWindow.FindAllDescendants(condition => condition.ByAutomationId(automationId)).Length;

    [Fact]
    public void AgentMode_GateRequiresBuild_ThenAgentAnswers()
    {
        var a = Path.Combine(_folder, "A.cs");
        File.WriteAllText(Path.Combine(_folder, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(_userData, "settings.json"),
            $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false, "agent.mode": "agent", "agent.approvals": "auto" }""");

        _model
            .CallTool("read_file", new { path = "A.cs" })
            .CallTool("apply_edits", new { edits = new[] { new { path = "A.cs", oldText = "class A", newText = "sealed class A" } } })
            .Reply("Готово.")
            .CallTool("build", new { })
            .Reply("Собрано.");

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "Сделай класс sealed");
        Keyboard.Type(VirtualKeyShort.ENTER);

        // A real build: the edited tab is saved first, otherwise the old code would be checked.
        Assert.True(Retry.WhileFalse(() => _model.Requests.Count == 5, TimeSpan.FromSeconds(90)).Result,
            $"Запросов к модели: {_model.Requests.Count}. Снимок: {session.SaveScreenshot("agent-gate-timeout")}");
        // The verification line is in the last activity block, collapsed after the turn; a click expands it.
        Retry.WhileFalse(() => session.MainWindow.FindAllDescendants(condition => condition.ByAutomationId("Agent.Activity")).Length > 0, TimeSpan.FromSeconds(10));
        session.MainWindow.FindAllDescendants(condition => condition.ByAutomationId("Agent.Activity"))[^1].GuardedClick();
        session.WaitFor("Agent.Status");
        session.SaveScreenshot("agent-gate");

        Assert.Equal("sealed class A { }\r\n", File.ReadAllText(a));
        Assert.Contains("не запускал build", _model.Requests.ToArray()[3], StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptEditsMode_ChangesPanel_RevertOneFile_AcceptTheRest()
    {
        var a = Path.Combine(_folder, "A.cs");
        var b = Path.Combine(_folder, "B.cs");
        File.WriteAllText(b, "class B { }\r\n");
        File.WriteAllText(Path.Combine(_userData, "settings.json"),
            $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false, "agent.approvals": "auto" }""");
        _model
            .CallTool("read_file", new { path = "A.cs" })
            .CallTool("read_file", new { path = "B.cs" })
            .CallTool("apply_edits", new
            {
                edits = new[]
                {
                    new { path = "A.cs", oldText = "class A", newText = "sealed class A" },
                    new { path = "B.cs", oldText = "class B", newText = "sealed class B" },
                },
            })
            .Reply("Запечатал оба класса.");

        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "Сделай классы sealed");
        Keyboard.Type(VirtualKeyShort.ENTER);

        // Edits applied without asking; the file list above the input is alphabetical, so A.cs comes first.
        session.WaitFor("Agent.Changes.Toggle");
        session.WaitForText("Agent.Changes.Toggle", text => text == "Изменено файлов: 2", TimeSpan.FromSeconds(10));
        session.Find("Agent.Changes.Diff").GuardedClick();
        session.WaitFor("Agent.Changes.DiffLines");
        session.SaveScreenshot("agent-changes");

        session.Find("Agent.Changes.Revert").GuardedClick();
        session.WaitForText("Agent.Changes.Toggle", text => text == "Изменено файлов: 1", TimeSpan.FromSeconds(10));
        session.Find("Agent.Changes.Accept").GuardedClick();

        session.WaitUntilGone("Agent.Changes");
        Assert.True(Retry.WhileFalse(() => File.ReadAllText(b) == "sealed class B { }\r\n", TimeSpan.FromSeconds(10)).Result, File.ReadAllText(b));
        Assert.Equal("class A { }\r\n", File.ReadAllText(a));
    }
}
