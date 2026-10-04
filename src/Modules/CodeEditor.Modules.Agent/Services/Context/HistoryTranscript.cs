using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// The summarized part of the history as file text (ADR 0023): user requests, model answers, calls with arguments and
/// full results. The model reads exact errors and code from it that the summary lacks. Reasoning and service data
/// (signatures, encrypted content) are skipped. One pass, O(n).
/// </summary>
internal static class HistoryTranscript
{
    public static string Render(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var text = new StringBuilder();
        ChatRole? role = null;
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                var part = Part(content);
                if (part is null)
                {
                    continue;
                }

                if (message.Role != role)
                {
                    role = message.Role;
                    text.Append(text.Length > 0 ? "\n" : string.Empty).Append("## ").AppendLine(Title(message.Role));
                }

                text.AppendLine(part);
            }
        }

        return text.ToString();
    }

    private static string? Part(AIContent content) => content switch
    {
        TextContent { Text: var value } when !string.IsNullOrWhiteSpace(value) => value,
        FunctionCallContent call => $"### {ToolCallText.Describe(call)} [{call.CallId}]",
        FunctionResultContent result => $"### Result [{result.CallId}]\n{ResultText(result)}",
        _ => null,
    };

    private static string Title(ChatRole role) =>
        role == ChatRole.Assistant ? "Assistant" : role == ChatRole.Tool ? "Tool results" : role == ChatRole.System ? "System" : "User";

    private static string ResultText(FunctionResultContent result) => result.Result switch
    {
        null => result.Exception?.Message ?? string.Empty,
        string value => value,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        JsonElement element => element.GetRawText(),
        var value => JsonSerializer.Serialize(value, AIJsonUtilities.DefaultOptions),
    };
}
