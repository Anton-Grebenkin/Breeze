using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Responses API without server-side conversation storage. By default a response is stored and the next request refers
/// to it by id; through ProxyAPI that response is sometimes "not found" (400 "Previous response … not found") and the
/// turn breaks. Here responses are not stored and the conversation id is not passed up: the agent sends the full
/// history, as with Chat Completions, and reasoning between steps travels encrypted. A service without encrypted
/// reasoning (Provod rejects both <c>store</c> and <c>include</c>) gets the request without these fields, and
/// <see cref="ResponsesReasoning"/> strips reasoning from history.
/// <para>
/// Role-less updates get the assistant role. The library emits reasoning without a role, so when the turn history was
/// rebuilt it stuck to the previous message (tool results, of which only the results are sent). After an approval,
/// editor note or new question the history lost reasoning from the second step on: the request prefix changed, the
/// cache was lost and the model forgot its reasoning.
/// </para>
/// </summary>
/// <param name="encryptedReasoning">Ask for an unstored response with encrypted reasoning.</param>
internal sealed class StatelessResponsesChatClient(IChatClient inner, bool encryptedReasoning = true) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, Stateless(options), cancellationToken);
        response.ConversationId = null;
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, Stateless(options), cancellationToken))
        {
            update.ConversationId = null;
            update.Role ??= ChatRole.Assistant;
            yield return update;
        }
    }

    private ChatOptions Stateless(ChatOptions? options)
    {
        var stateless = options?.Clone() ?? new ChatOptions();
        stateless.ConversationId = null;
        if (!encryptedReasoning)
        {
            return stateless;
        }

        stateless.RawRepresentationFactory ??= _ => new CreateResponseOptions
        {
            StoredOutputEnabled = false,
            IncludedProperties = { IncludedResponseProperty.ReasoningEncryptedContent },
        };
        return stateless;
    }
}
