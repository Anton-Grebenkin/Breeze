using System.Text;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>The proxy's stand-in for an empty Claude answer never reaches the user or hides an empty result.</summary>
public sealed class ProxyPlaceholderChatClientTests
{
    [Theory]
    [InlineData("[System: Empty message |content sanitised to satisfied protocol]", "")]
    [InlineData("\n|[System: Empty message content sanitised to satisfied protocol]", "\n")]
    [InlineData("[Sys|tem] — обычный ответ", "[System] — обычный ответ")]
    [InlineData("Готово.", "Готово.")]
    public async Task Placeholder_IsDropped_OtherTextPasses(string fragments, string expected)
    {
        using var inner = new ScriptedChatClient().Reply(fragments.Split('|'));
        using var client = new ProxyPlaceholderChatClient(inner);

        var text = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "?")], cancellationToken: TestContext.Current.CancellationToken))
        {
            text.Append(update.Text);
        }

        Assert.Equal(expected, text.ToString());
    }

    [Fact]
    public void PlaceholderInHistory_IsDroppedLikeEmptyText()
    {
        var sent = EmptyContentFilterChatClient.Clean([new ChatMessage(ChatRole.Assistant, ProxyPlaceholderChatClient.Placeholder), new ChatMessage(ChatRole.User, "дальше")]);

        Assert.Equal("дальше", Assert.Single(sent).Text);
    }
}
