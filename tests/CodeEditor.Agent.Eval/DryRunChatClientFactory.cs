using System.Runtime.CompilerServices;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Model for <c>--dry-run</c>: answers "done" to any request at no cost. It exercises the harness (container,
/// repository, turn, checks and report) without solving tasks.
/// </summary>
internal sealed class DryRunChatClientFactory : IChatClientFactory
{
    public const string Answer = "Готово (пробный прогон без модели).";

    public IChatClient Create(AgentOptions options) => new Client();

    private sealed class Client : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, Answer) { FinishReason = ChatFinishReason.Stop };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
