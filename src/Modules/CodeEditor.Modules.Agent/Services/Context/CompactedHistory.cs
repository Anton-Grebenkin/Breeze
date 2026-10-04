using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// What replaces the compacted part of the history (ADR 0023). User requests stay verbatim in a separate message:
/// a retelling loses the requirements the audit relies on (ADR 0016). They are taken newest first, up to ~20K tokens.
/// The model's summary comes with the exact harness state and the path to the full history. Requests from earlier
/// compactions carry over into the next one.
/// </summary>
internal static class CompactedHistory
{
    /// <summary>About 20K tokens of user requests.</summary>
    public const int RequestsBudgetCharacters = 60_000;

    public const string RequestsHeader =
        "<earlier_requests>\nThe earlier part of this chat was compacted. The user's requests from it, verbatim, oldest first:";

    public const string RequestsFooter = "</earlier_requests>";

    // A user message built by the editor ends with a reminder; its first part is the user's text (TurnMessageBuilder).
    private const string ReminderPrefix = "<reminder>";

    private const string AnswerPrefix = "Answer to the agent's question: ";

    /// <summary>User requests in order: their messages, answers to agent questions and requests of earlier compactions.</summary>
    public static List<string> Requests(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var questions = messages.SelectMany(static message => message.Contents).OfType<FunctionCallContent>()
            .Where(static call => call.Name == WorkflowAgentTools.AskUserName)
            .Select(static call => call.CallId)
            .ToHashSet(StringComparer.Ordinal);
        return [.. messages.SelectMany(message => RequestsOf(message, questions))];
    }

    /// <returns>A message with the newest requests that fit; <c>null</c> if there are none.</returns>
    public static ChatMessage? RequestsMessage(IReadOnlyList<string> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var kept = new List<string>();
        var left = RequestsBudgetCharacters;
        for (var index = requests.Count - 1; index >= 0 && left > 0; index--)
        {
            var request = requests[index].Length <= left ? requests[index] : string.Concat(requests[index].AsSpan(0, left), "…");
            kept.Add(request);
            left -= request.Length;
        }

        if (kept.Count == 0)
        {
            return null;
        }

        kept.Reverse();
        return new ChatMessage(ChatRole.User, [new TextContent(RequestsHeader), .. kept.Select(static request => new TextContent(request)), new TextContent(RequestsFooter)]);
    }

    /// <param name="state">Exact harness state (<see cref="ContextCompaction.HarnessState"/>); empty if none.</param>
    /// <param name="transcript">Line about the full history file; <c>null</c> if saving failed.</param>
    public static ChatMessage SummaryMessage(string summary, string state, string? transcript)
    {
        string[] parts = ["[Summary]\n" + summary.Trim(), state, transcript ?? string.Empty];
        var message = new ChatMessage(ChatRole.Assistant, string.Join("\n\n", parts.Where(static part => part.Length > 0)));
        (message.AdditionalProperties ??= [])[CompactionMessageGroup.SummaryPropertyKey] = true;
        return message;
    }

    private static IEnumerable<string> RequestsOf(ChatMessage message, HashSet<string> questions)
    {
        if (message.Role == ChatRole.Tool || message.Contents.Any(static content => content is FunctionResultContent))
        {
            return message.Contents.OfType<FunctionResultContent>()
                .Where(result => questions.Contains(result.CallId) && result.Result is not null)
                .Select(result => AnswerPrefix + result.Result);
        }

        if (message.Role != ChatRole.User)
        {
            return [];
        }

        string[] texts = [.. message.Contents.OfType<TextContent>().Select(static content => content.Text)];
        return texts switch
        {
            [RequestsHeader, .. var carried, RequestsFooter] => carried,
            [var request, ..] when texts.Any(static text => text.StartsWith(ReminderPrefix, StringComparison.Ordinal)) => [request],
            _ => [],
        };
    }
}
