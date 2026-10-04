using System.Text;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// The work log as text for the advisor (ADR 0012): user requests, model notes, tool calls and shortened results; tool
/// output fills most of the history, and the advisor needs only its start and end. Service blocks (context, reminder,
/// snapshot, memory) are skipped. A long log keeps the latest parts; the first user request is always kept.
/// O(n) in message length.
/// </summary>
public static class TranscriptDigest
{
    public const int MaxCharacters = 60_000;
    public const int ResultHead = 800;
    public const int ResultTail = 400;
    public const int ArgumentsLength = 300;

    private static readonly string[] ServiceBlocks = ["<context>", "<reminder>", "<workspace>", "<memory>"];

    public static string Render(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var parts = messages.Where(static message => message.Role != ChatRole.System).SelectMany(Describe).ToList();
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        // The first part plus the newest parts that fit, collected backwards and reversed once.
        var kept = new List<string> { parts[0] };
        var length = parts[0].Length;
        for (var index = parts.Count - 1; index > 0 && length + parts[index].Length <= MaxCharacters; index--)
        {
            kept.Add(parts[index]);
            length += parts[index].Length;
        }

        kept.Reverse(1, kept.Count - 1);
        return string.Join("\n\n", kept);
    }

    /// <summary>The user's chat requests in order, as the reviewer's task; if too long, the latest ones.</summary>
    public static string UserRequests(IReadOnlyList<ChatMessage> messages, int maxCharacters = 8_000)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var requests = string.Join("\n\n", messages
            .Where(static message => message.Role == ChatRole.User)
            .SelectMany(static message => message.Contents.OfType<TextContent>())
            .Select(static content => content.Text.Trim())
            .Where(static text => text.Length > 0 && !IsServiceBlock(text)));
        return requests.Length <= maxCharacters ? requests : "…" + requests[^maxCharacters..];
    }

    private static IEnumerable<string> Describe(ChatMessage message)
    {
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent text when !IsServiceBlock(text.Text) && !string.IsNullOrWhiteSpace(text.Text):
                    yield return (message.Role == ChatRole.User ? "USER: " : "AGENT: ") + text.Text.Trim();
                    break;
                case FunctionCallContent call:
                    yield return "CALL " + Shorten(ToolCallText.Describe(call), ArgumentsLength);
                    break;
                case FunctionResultContent result:
                    yield return "RESULT: " + ToolResultPreview(result.Result?.ToString() ?? string.Empty);
                    break;
            }
        }
    }

    private static bool IsServiceBlock(string text) => ServiceBlocks.Any(block => text.StartsWith(block, StringComparison.Ordinal));

    private static string ToolResultPreview(string result) =>
        result.Length <= ResultHead + ResultTail
            ? result
            : new StringBuilder(result, 0, ResultHead, ResultHead + ResultTail + 8).Append(" […] ").Append(result, result.Length - ResultTail, ResultTail).ToString();

    private static string Shorten(string text, int length) => text.Length <= length ? text : string.Concat(text.AsSpan(0, length), "…");
}
