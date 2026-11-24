using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// The user's own connection, end to end (ADR 0055): with no key and no settings, a gateway is added through the
/// connections panel — address, key, check — its model is switched on, described and chosen, and the agent answers
/// through it. Nothing is written to the settings file by hand, and the key is not in it.
/// </summary>
public sealed class AgentConnectionsTests : IDisposable
{
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromSeconds(30);

    private readonly FakeOpenAIServer _gateway = new() { Models = ["team-pro", "team-fast"] };
    private readonly string _userData = AppSession.CreateUserDataFolder();
    private readonly string _folder;

    public AgentConnectionsTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "A.cs"), "class A { }\r\n");
        File.WriteAllText(Path.Combine(_userData, "settings.json"), """{ "agent.autoMemory": false }""");
    }

    public void Dispose()
    {
        _gateway.Dispose();
        AppSession.DeleteQuietly(_userData);
        AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);
    }

    [Fact]
    public void Gateway_IsAddedThroughThePanel_ItsModelIsDescribed_AndAnswers()
    {
        _gateway.Reply("Ответ шлюза.");
        using var session = AppSession.WithUserData(_userData, _folder);
        session.WaitFor("Explorer.Node.A.cs");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);

        // No key yet: the chat offers the built-in service's key or a connection of the user's own.
        session.WaitFor("Agent.AddConnection").GuardedClick();
        session.WaitFor("Agent.Connections.Empty");

        session.Find("Agent.Connections.New.Name").GuardedClick();
        session.TypeInto("Agent.Connections.New.Name", "Gateway");
        session.Find("Agent.Connections.New.Endpoint").GuardedClick();
        session.TypeInto("Agent.Connections.New.Endpoint", _gateway.Endpoint);
        session.Find("Agent.Connections.New.Key").GuardedClick();
        Keyboard.Type("test-key");
        session.Find("Agent.Connections.New.Add").GuardedClick();

        // The service's models are listed; one is switched on, said to be DeepSeek and chosen.
        session.WaitFor("Agent.Connections.Model.team-pro").GuardedClick();
        session.WaitFor("Agent.Connections.Family.team-pro").GuardedClick();
        session.WaitFor("Agent.Connections.FamilyChoice.5").GuardedClick();
        session.WaitForText("Agent.Connections.Family.team-pro", title => title == "DeepSeek", ModelTimeout);
        session.SaveScreenshot("agent-connections");
        session.Find("Agent.Connections.Use.team-pro").GuardedClick();
        session.Find("Agent.Connections.Back").GuardedClick();

        session.WaitForFocus("Agent.Input");
        session.TypeInto("Agent.Input", "Привет");
        Keyboard.Type(VirtualKeyShort.ENTER);

        Assert.True(Retry.WhileFalse(() => _gateway.Requests.Count == 1, ModelTimeout).Result, $"Запросов к шлюзу: {_gateway.Requests.Count}");
        Assert.True(_gateway.Requests.TryPeek(out var request));
        Assert.Contains("\"model\":\"team-pro\"", request, StringComparison.Ordinal);

        var settings = File.ReadAllText(Path.Combine(_userData, "settings.json"));
        Assert.Contains("\"agent.model\": \"gateway:team-pro\"", settings, StringComparison.Ordinal);
        Assert.Contains("\"family\": \"deepSeek\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", settings, StringComparison.Ordinal);
    }
}
