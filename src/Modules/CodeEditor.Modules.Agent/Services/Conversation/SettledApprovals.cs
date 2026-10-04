using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Removes settled approvals from model history. Agent Framework keeps an approval request in the model reply and the
/// response as a separate user message; the tool loop strips them before a model request, but only until the call is
/// marked executed. After ordinary steps in a run the mark is set, so on the next approval the old ones stayed in the
/// request: empty user messages, a different GPT reply phase, and a different request prefix that broke the cache.
/// After a turn, approvals with a result are removed (a message left empty goes entirely), so history between runs
/// matches what the model saw within a run. A pending approval stays.
/// </summary>
public static class SettledApprovals
{
    public static void Remove(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.TryGetInMemoryChatHistory(out var history) && Settle(history) is { } settled)
        {
            session.SetInMemoryChatHistory(settled);
        }
    }

    /// <summary>History without settled approvals; <c>null</c> if nothing to remove. O(n) in the number of contents.</summary>
    /// <param name="answered">
    /// Also remove approved but unexecuted calls, after a stopped or failed turn; otherwise the next turn would run them
    /// silently.
    /// </param>
    public static List<ChatMessage>? Settle(IReadOnlyList<ChatMessage> history, bool answered = false)
    {
        ArgumentNullException.ThrowIfNull(history);
        var done = history.SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                FunctionResultContent result => result.CallId,
                ToolApprovalResponseContent { ToolCall: FunctionCallContent call } when answered => call.CallId,
                _ => null,
            })
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        List<ChatMessage>? settled = null;
        for (var index = 0; index < history.Count; index++)
        {
            var message = history[index];
            if (!message.Contents.Any(content => IsSettled(content, done)))
            {
                settled?.Add(message);
                continue;
            }

            settled ??= [.. history.Take(index)];
            if (message.Contents.Where(content => !IsSettled(content, done)).ToList() is { Count: > 0 } kept)
            {
                var copy = message.Clone();
                copy.Contents = kept;
                settled.Add(copy);
            }
        }

        return settled;
    }

    private static bool IsSettled(AIContent content, HashSet<string> done) => content switch
    {
        ToolApprovalRequestContent { ToolCall: FunctionCallContent call } => done.Contains(call.CallId),
        ToolApprovalResponseContent { ToolCall: FunctionCallContent call } => done.Contains(call.CallId),
        _ => false,
    };
}
