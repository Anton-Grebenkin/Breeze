using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Anthropic;

/// <summary>
/// Converts chat history to Anthropic Messages blocks. Leading system messages go to <c>system</c>, then <c>user</c> and
/// <c>assistant</c> turns alternate: tool results go in a user turn, adjacent messages of one role merge into one turn,
/// and results come first in a turn (Anthropic requires it). Reasoning is sent back as a <c>thinking</c> block only
/// with its signature (<see cref="TextReasoningContent.ProtectedData"/>); Anthropic rejects it otherwise. Empty text is
/// not sent: Anthropic rejects it too. One pass over the history, O(n).
/// </summary>
internal static class AnthropicMessages
{
    /// <summary>Signature prefix marking redacted reasoning (<c>redacted_thinking</c>): the rest is its data, not a signature.</summary>
    public const string RedactedPrefix = "redacted:";

    public static (JsonArray System, JsonArray Turns) Convert(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var system = new JsonArray();
        var turns = new List<(string Role, List<JsonObject> Blocks)>();
        var leading = true;
        foreach (var message in messages)
        {
            if (leading && message.Role == ChatRole.System)
            {
                AddText(system, message.Text);
                continue;
            }

            leading = false;
            var assistant = message.Role == ChatRole.Assistant;
            var blocks = assistant ? AssistantBlocks(message) : UserBlocks(message);
            if (blocks.Count == 0)
            {
                continue;
            }

            var role = assistant ? "assistant" : "user";
            if (turns.Count > 0 && turns[^1].Role == role)
            {
                turns[^1].Blocks.AddRange(blocks);
            }
            else
            {
                turns.Add((role, blocks));
            }
        }

        return (system, new JsonArray([.. turns.Select(Turn)]));
    }

    private static JsonObject Turn((string Role, List<JsonObject> Blocks) turn)
    {
        // In a user turn tool results precede text; order within each group is kept (stable sort).
        var blocks = turn.Role == "user" ? turn.Blocks.OrderBy(block => (string?)block["type"] == "tool_result" ? 0 : 1) : turn.Blocks.AsEnumerable();
        return new JsonObject { ["role"] = turn.Role, ["content"] = new JsonArray([.. blocks]) };
    }

    private static List<JsonObject> UserBlocks(ChatMessage message)
    {
        var blocks = new List<JsonObject>();
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent { Text: var text } when !string.IsNullOrWhiteSpace(text):
                    blocks.Add(Text(text));
                    break;
                case DataContent data when data.HasTopLevelMediaType("image"):
                    blocks.Add(Image(new JsonObject { ["type"] = "base64", ["media_type"] = data.MediaType, ["data"] = data.Base64Data.ToString() }));
                    break;
                case UriContent uri when uri.HasTopLevelMediaType("image"):
                    blocks.Add(Image(new JsonObject { ["type"] = "url", ["url"] = uri.Uri.ToString() }));
                    break;
                case FunctionResultContent result:
                    blocks.Add(ToolResult(result));
                    break;
            }
        }

        return blocks;
    }

    private static List<JsonObject> AssistantBlocks(ChatMessage message)
    {
        var blocks = new List<JsonObject>();
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextReasoningContent { ProtectedData: { Length: > 0 } signature } reasoning:
                    blocks.Add(Thinking(reasoning.Text, signature));
                    break;
                case TextContent { Text: var text } when !string.IsNullOrWhiteSpace(text):
                    blocks.Add(Text(text));
                    break;
                case FunctionCallContent call:
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = call.CallId,
                        ["name"] = call.Name,
                        ["input"] = JsonSerializer.SerializeToNode(call.Arguments ?? new Dictionary<string, object?>(), AIJsonUtilities.DefaultOptions),
                    });
                    break;
            }
        }

        return blocks;
    }

    private static JsonObject Thinking(string text, string signature) => signature.StartsWith(RedactedPrefix, StringComparison.Ordinal)
        ? new JsonObject { ["type"] = "redacted_thinking", ["data"] = signature[RedactedPrefix.Length..] }
        : new JsonObject { ["type"] = "thinking", ["thinking"] = text, ["signature"] = signature };

    private static JsonObject ToolResult(FunctionResultContent result)
    {
        var block = new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = result.CallId, ["content"] = ResultText(result) };
        if (result.Exception is not null)
        {
            block["is_error"] = true;
        }

        return block;
    }

    private static string ResultText(FunctionResultContent result) => result.Result switch
    {
        null => result.Exception?.Message ?? string.Empty,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        JsonElement element => element.GetRawText(),
        var value => JsonSerializer.Serialize(value, AIJsonUtilities.DefaultOptions),
    };

    private static void AddText(JsonArray blocks, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            blocks.Add(Text(text));
        }
    }

    private static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };

    private static JsonObject Image(JsonObject source) => new() { ["type"] = "image", ["source"] = source };
}
