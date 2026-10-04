using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// Model manager: opens from the palette, gets the list from the service (<c>GET /models</c> of the fake model), a
/// mouse toggle writes <c>agent.models</c>, Esc returns to the chat.
/// </summary>
public sealed class AgentModelsTests : IDisposable
{
    private const string ManageModelsCommand = "agent.manageModels";

    private readonly FakeOpenAIServer _model = new() { Models = ["test/model", "openai/gpt-6-luna", "x-ai/grok-4.7", "qwen/qwen3-coder-plus"] };
    private readonly string _userData = AppSession.CreateUserDataFolder();

    public AgentModelsTests()
    {
        File.WriteAllText(Path.Combine(_userData, "settings.json"),
            $$"""{ "agent.endpoint": "{{_model.Endpoint}}", "agent.model": "test/model", "agent.autoMemory": false, "agent.models": ["test/model"] }""");
        new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(_userData)).Set("agent-api-key", "test-key");
    }

    public void Dispose()
    {
        _model.Dispose();
        AppSession.DeleteQuietly(_userData);
    }

    [Fact(Skip = "Менеджер моделей скрыт из палитры и меню, пока выбор ограничен GPT, Claude и Grok (01.10.2026).")]
    public void Manager_ListsServiceModels_ToggleWritesSetting_EscapeReturnsToChat()
    {
        using var session = AppSession.WithUserData(_userData);
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        session.WaitForFocus(AutomationIds.PaletteQuery);
        session.TypeInto(AutomationIds.PaletteQuery, "управление моделями");
        session.WaitFor(AutomationIds.PaletteItem(ManageModelsCommand));
        Keyboard.Type(VirtualKeyShort.ENTER);

        session.WaitFor("Agent.Models");
        session.WaitForText("Agent.Models.Status", text => text.StartsWith("Моделей: 4", StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        session.Find("Agent.Models.Vendor.x-ai").GuardedClick();
        session.WaitFor("Agent.Models.Toggle.x-ai/grok-4.7").GuardedClick();
        session.SaveScreenshot("agent-models");

        var settings = Path.Combine(_userData, "settings.json");
        Assert.True(Retry.WhileFalse(() => File.ReadAllText(settings).Contains("x-ai/grok-4.7", StringComparison.Ordinal), TimeSpan.FromSeconds(5)).Result, File.ReadAllText(settings));

        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone("Agent.Models");
        session.WaitFor("Agent.Input");
    }
}
