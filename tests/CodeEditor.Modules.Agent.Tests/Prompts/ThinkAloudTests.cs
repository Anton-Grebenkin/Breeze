using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Prompts;

/// <summary>
/// A model without native reasoning thinks aloud (ADR 0010): the prompt and reminder ask for tags, the feed shows the
/// tagged text as reasoning and the answer without tags. A native-reasoning model gets no such requests.
/// </summary>
public sealed class ThinkAloudTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ModelWithoutOwnReasoning_TaggedThoughtsShownAsReasoning_AnswerClean()
    {
        _fixture.Options.Set(new AgentOptions { Model = "mistralai/devstral-2512" });
        _fixture.Client.Reply("<thin", "king>Нужно сложить ", "числа.</thinking>\n\n", "Будет 46.");

        await _fixture.SendAsync("сколько будет 3 * 17 - 5?");

        var messages = _fixture.Chat.Messages;
        var thought = Assert.Single(messages, message => message.Kind == ChatMessageKind.Reasoning);
        Assert.Equal("Нужно сложить числа.", thought.Text);
        Assert.False(thought.IsInProgress);
        Assert.Equal("Будет 46.", messages[^1].Text);
        Assert.Contains("## Thinking out loud", Instructions(), StringComparison.Ordinal);
        Assert.Contains(PromptSections.ThinkAloudReminder, LastUserText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModelWithOwnReasoning_NotAskedToThinkAloud()
    {
        _fixture.Options.Set(new AgentOptions { Model = "deepseek/deepseek-v4-flash" });
        _fixture.Client.Reply("Ответ.");

        await _fixture.SendAsync("вопрос");

        Assert.DoesNotContain("## Thinking out loud", Instructions(), StringComparison.Ordinal);
        Assert.DoesNotContain(PromptSections.ThinkAloudReminder, LastUserText(), StringComparison.Ordinal);
    }

    private string LastUserText() =>
        string.Concat(_fixture.Client.Requests[^1].Last(message => message.Role == ChatRole.User).Contents.OfType<TextContent>().Select(content => content.Text));

    private string Instructions() =>
        _fixture.Client.LastOptions?.Instructions
        ?? string.Join('\n', _fixture.Client.Requests[^1].Where(message => message.Role == ChatRole.System).Select(message => message.Text));
}
