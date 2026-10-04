using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Saves a stopped or failed turn into the model history. Agent Framework writes session history only at the end of a
/// successful turn, so after Stop or a service error (402, network, 5xx) the model remembered neither the question nor
/// the calls made, and "continue" started from scratch. Here the history gets what the model has already seen: the last
/// request's messages after the prefix shared with history (question, tool calls and results, without the broken
/// reply) and a note on why the turn ended. A run after an approval starts not with the question but with the approval
/// response, which is no longer in the request; a call with a result gives it away. Approved but unexecuted calls are
/// removed from history (<see cref="SettledApprovals"/>) so the next turn does not run them silently.
/// </summary>
public static class StoppedTurnHistory
{
    /// <summary>
    /// Note for the model: the next user request does not continue the broken reply. An interrupted command may have
    /// completed partially and background ones keep running (as in Codex, <c>&lt;turn_aborted&gt;</c>).
    /// </summary>
    public const string StoppedNote = "(The user stopped this turn here. A command that was running may have completed partially, and background commands may still be running — check their state before relying on it.)";

    /// <summary>Note for the model: a service error interrupted the turn; the work above was done.</summary>
    public const string FailedNote = "(This turn was interrupted here by an error of the model service; the work above was done.)";

    /// <param name="lastRequest">Messages of the last model request: history plus the turn's new messages.</param>
    /// <param name="message">The message that started the turn; if no model request was made, only it is kept.</param>
    /// <param name="note">Why the turn ended: <see cref="StoppedNote"/> or <see cref="FailedNote"/>.</param>
    public static void Keep(AgentSession session, IReadOnlyList<ChatMessage> lastRequest, ChatMessage message, string note = StoppedNote)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lastRequest);
        ArgumentNullException.ThrowIfNull(message);
        if (!session.TryGetInMemoryChatHistory(out var history))
        {
            history = [];
        }

        var start = IsFromTurn(lastRequest, message) ? CommonStart(history, lastRequest) : -1;
        List<ChatMessage> kept = start >= 0 ? [.. history.Take(start), .. lastRequest.Skip(start)] : [.. history, message];
        kept.Add(new ChatMessage(ChatRole.Assistant, note));
        session.SetInMemoryChatHistory(SettledApprovals.Settle(kept, answered: true) ?? kept);
    }

    // The request belongs to this turn if it holds the turn message or, after an approval, results of the approved calls.
    private static bool IsFromTurn(IReadOnlyList<ChatMessage> lastRequest, ChatMessage message)
    {
        if (lastRequest.Any(request => ReferenceEquals(request, message)))
        {
            return true;
        }

        var approved = message.Contents.OfType<ToolApprovalResponseContent>().Select(response => response.ToolCall.CallId).ToList();
        var results = lastRequest.SelectMany(request => request.Contents).OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet(StringComparer.Ordinal);
        return approved.Count > 0 && approved.All(results.Contains);
    }

    // How many leading history messages the model saw unchanged; past them the request may have rewritten them (approval).
    private static int CommonStart(IReadOnlyList<ChatMessage> history, IReadOnlyList<ChatMessage> lastRequest)
    {
        var count = 0;
        while (count < history.Count && count < lastRequest.Count && ReferenceEquals(history[count], lastRequest[count]))
        {
            count++;
        }

        return count;
    }
}
