using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>
/// Fake model: answers each request with the next scripted response, fragment by fragment. <see cref="Gate"/> holds
/// the stream until signaled, to test stopping mid-answer.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<IEnumerable<ChatResponseUpdate>>> _responses = new();

    public List<List<ChatMessage>> Requests { get; } = [];

    public ChatOptions? LastOptions { get; private set; }

    /// <summary>Options of each request, in the same order as <see cref="Requests"/>.</summary>
    public List<ChatOptions?> RequestOptions { get; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public const string WrapUpAnswer = "Итог: лимит шагов.";

    public bool IsDisposed { get; private set; }

    public ScriptedChatClient Reply(params string[] fragments)
    {
        _responses.Enqueue(() => fragments.Select(fragment => new ChatResponseUpdate(ChatRole.Assistant, fragment)));
        return this;
    }

    /// <summary>An answer followed by token usage at the end of the stream, as services send it.</summary>
    public ScriptedChatClient ReplyWithUsage(string text, long inputTokens, long outputTokens)
    {
        _responses.Enqueue(() =>
        [
            new ChatResponseUpdate(ChatRole.Assistant, text),
            new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens })]),
        ]);
        return this;
    }

    /// <summary>An answer with several cumulative usage reports, as ProxyAPI sends them for Claude.</summary>
    public ScriptedChatClient ReplyWithUsages(string text, params (long Input, long Output)[] usages)
    {
        _responses.Enqueue(() =>
        [
            new ChatResponseUpdate(ChatRole.Assistant, text),
            .. usages.Select(usage => new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = usage.Input, OutputTokenCount = usage.Output })])),
        ]);
        return this;
    }

    public ScriptedChatClient CallTool(string callId, string name, IDictionary<string, object?> arguments)
    {
        _responses.Enqueue(() => [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)])]);
        return this;
    }

    /// <summary>A tool call followed by token usage at the end of the stream, as services send it.</summary>
    public ScriptedChatClient CallToolWithUsage(string callId, string name, IDictionary<string, object?> arguments, long inputTokens)
    {
        _responses.Enqueue(() =>
        [
            new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]),
            new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = 10 })]),
        ]);
        return this;
    }

    /// <summary>A preamble and a tool call in one answer, as GPT-5 models write before tools.</summary>
    public ScriptedChatClient SayAndCallTool(string text, string callId, string name, IDictionary<string, object?> arguments)
    {
        _responses.Enqueue(() =>
        [
            new ChatResponseUpdate(ChatRole.Assistant, text),
            new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]),
        ]);
        return this;
    }

    /// <summary>Several tool calls in one answer, which may run in parallel.</summary>
    public ScriptedChatClient CallTools(params (string CallId, string Name, IDictionary<string, object?> Arguments)[] calls)
    {
        _responses.Enqueue(() => [new ChatResponseUpdate(ChatRole.Assistant, [.. calls.Select(call => new FunctionCallContent(call.CallId, call.Name, call.Arguments))])]);
        return this;
    }

    /// <summary>An answer cut off by the length limit.</summary>
    public ScriptedChatClient ReplyTruncated(string text)
    {
        _responses.Enqueue(() => [new ChatResponseUpdate(ChatRole.Assistant, text) { FinishReason = ChatFinishReason.Length }]);
        return this;
    }

    public ScriptedChatClient Fail(Exception exception)
    {
        _responses.Enqueue(() => throw exception);
        return this;
    }

    /// <summary>Non-streaming helper requests get the same scripted response as a whole.</summary>
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        RequestOptions.Add(options);
        LastOptions = options;

        // Tools disabled (the last step of the budget): the model wraps up without consuming a scripted response.
        var updates = options?.ToolMode is NoneChatToolMode ? [new ChatResponseUpdate(ChatRole.Assistant, WrapUpAnswer)] : _responses.Dequeue()();
        var first = true;
        foreach (var update in updates)
        {
            if (!first && Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            first = false;
            await Task.Yield();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => IsDisposed = true;
}
