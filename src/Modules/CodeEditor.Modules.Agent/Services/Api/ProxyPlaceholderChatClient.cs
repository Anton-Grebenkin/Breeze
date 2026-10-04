using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Removes the proxy placeholder from responses: LiteLLM at ProxyAPI substitutes "[System: Empty message content
/// sanitised…]" for an empty Claude reply, which users saw as the agent's answer and the empty-result check missed.
/// While the reply text still matches the placeholder prefix, updates are held back (at most the placeholder length);
/// on a full match its text is dropped and other content passes through.
/// </summary>
internal sealed class ProxyPlaceholderChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public const string Placeholder = "[System: Empty message content sanitised to satisfied protocol]";

    public static bool IsPlaceholder(string text) => text.Trim().Equals(Placeholder, StringComparison.Ordinal);

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            RemoveText(message.Contents, content => IsPlaceholder(content.Text));
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var held = new List<ChatResponseUpdate>();
        var text = new StringBuilder();
        var deciding = true;
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (!deciding)
            {
                yield return update;
                continue;
            }

            held.Add(update);
            text.Append(update.Text);
            var start = text.ToString().TrimStart();
            // Reasoning before any text is not held: there is nothing to decide yet.
            if (start.Length > 0)
            {
                if (start.Length < Placeholder.Length && Placeholder.StartsWith(start, StringComparison.Ordinal))
                {
                    continue;
                }

                deciding = false;
                if (IsPlaceholder(start))
                {
                    held.ForEach(chunk => RemoveText(chunk.Contents, _ => true));
                }
            }

            foreach (var chunk in held)
            {
                yield return chunk;
            }

            held.Clear();
        }

        foreach (var chunk in held)
        {
            yield return chunk;
        }
    }

    private static void RemoveText(IList<AIContent> contents, Func<TextContent, bool> predicate)
    {
        for (var i = contents.Count - 1; i >= 0; i--)
        {
            if (contents[i] is TextContent text && predicate(text))
            {
                contents.RemoveAt(i);
            }
        }
    }
}
