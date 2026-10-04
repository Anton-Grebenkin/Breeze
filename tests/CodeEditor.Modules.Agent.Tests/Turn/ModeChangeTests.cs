using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// A mode switch between requests is announced to the model; otherwise, after Ask mode, "make that change" got an answer
/// with code instead of an edit.
/// </summary>
public sealed class ModeChangeTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ModeSwitch_IsAnnouncedOnce_NewChatForgetsIt()
    {
        Use(AgentMode.Ask);
        _fixture.Client.Reply("Причина — локаль.").Reply("Правлю.").Reply("Готово.").Reply("Новый чат.");
        await _fixture.SendAsync("в чём причина?");
        Use(AgentMode.Agent);

        await _fixture.SendAsync("внеси эту правку");
        var switched = LastReminder();
        await _fixture.SendAsync("спасибо");
        var same = LastReminder();
        Use(AgentMode.Ask);
        _fixture.Chat.NewChatCommand.Execute(null);
        await _fixture.SendAsync("новый вопрос");

        Assert.Contains("switched from Ask mode to Agent mode: you may change files now", switched, StringComparison.Ordinal);
        Assert.DoesNotContain("switched", same, StringComparison.Ordinal);
        Assert.DoesNotContain("switched", LastReminder(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentMode.Ask, AgentMode.Agent, "Make the changes discussed above with tools")]
    [InlineData(AgentMode.Agent, AgentMode.Plan, "don't change files from now on")]
    [InlineData(AgentMode.Ask, AgentMode.Plan, "from Ask mode to Plan mode.")]
    public void ModeChange_SaysWhatTheNewModeAllows(AgentMode previous, AgentMode current, string expected) =>
        Assert.Contains(expected, PromptSections.ModeChange(previous, current), StringComparison.Ordinal);

    private void Use(AgentMode mode) => _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = mode, AutoMemory = false });

    private string LastReminder() =>
        _fixture.Client.Requests[^1].Last(message => message.Role == ChatRole.User).Contents.OfType<TextContent>().Last().Text;
}
