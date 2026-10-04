using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Anthropic;

/// <summary>
/// Claude prompt cache. Anthropic does not cache by itself: without <c>cache_control</c> marks every agent step pays
/// full input price for the whole history. A mark writes a cache entry (the request prefix up to and including the
/// block), and a read looks for earlier entries at most 20 blocks back from each mark. Hence several marks (Anthropic
/// allows up to 4):
/// <list type="bullet">
/// <item>on the system prompt: tool schemas and the prompt;</item>
/// <item>on the end of history: a sliding point, as in Claude Code; the next step reads everything before it;</item>
/// <item>on the two turns before the end (the model reply and the previous user turn), where the previous request and
/// warm-up (<see cref="CacheWarmupChatClient"/>) wrote entries: an exact hit even if a step added more than 20 blocks
/// (many calls at once).</item>
/// </list>
/// A read costs 0.1× input, a write 1.25×, a one-hour write 2×. Chat Completions bodies are edited by
/// <see cref="JsonBodyPolicy"/>; Anthropic Messages requests are marked via <see cref="MarkConversation"/>.
/// </summary>
internal static class AnthropicCache
{
    /// <summary>Marked history turns: the end, the model reply before it and the previous user turn.</summary>
    private const int MarkedTurns = 3;

    /// <summary>Adds marks to a Chat Completions body; leaves other requests alone.</summary>
    public static bool Mark(JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body["messages"] is not JsonArray { Count: > 0 } messages)
        {
            return false;
        }

        if (messages.LastOrDefault(item => Role(item) == "system") is JsonObject system)
        {
            MarkMessage(system);
        }

        // The previous request ended right before the last model reply: the step appended the reply and call results.
        var answer = LastIndexOf(messages, "assistant");
        if (answer > 0 && messages[answer - 1] is JsonObject previous && Role(previous) != "system")
        {
            MarkMessage(previous);
        }

        if (messages[^1] is JsonObject last)
        {
            MarkMessage(last);
        }

        return true;
    }

    /// <summary>Messages request marks: the system prompt and the last three history turns, 5 minutes each.</summary>
    public static void MarkConversation(JsonArray system, JsonArray turns)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(turns);
        MarkLast(system);
        foreach (var turn in turns.TakeLast(MarkedTurns))
        {
            MarkTurn(turn, hour: false);
        }
    }

    /// <summary>
    /// One-hour warm-up marks: the system prompt, the end of the previous request and the model reply. The last turn is
    /// a stub the next request will not repeat, so it gets no mark. Anthropic requires one-hour marks before shorter
    /// ones, so all of them are one-hour.
    /// </summary>
    public static void MarkWarmUp(JsonArray system, JsonArray turns)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(turns);
        MarkLast(system, hour: true);
        foreach (var turn in turns.SkipLast(1).TakeLast(MarkedTurns - 1))
        {
            MarkTurn(turn, hour: true);
        }
    }

    /// <summary>
    /// Marks the last content block, caching everything up to and including it. A reasoning block cannot be marked, so
    /// such a turn stays unmarked.
    /// </summary>
    public static void MarkLast(JsonArray blocks, bool hour = false)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks is [.., JsonObject last] && (string?)last["type"] is not ("thinking" or "redacted_thinking"))
        {
            last["cache_control"] = Ephemeral(hour);
        }
    }

    private static void MarkTurn(JsonNode? turn, bool hour)
    {
        if (turn?["content"] is JsonArray blocks)
        {
            MarkLast(blocks, hour);
        }
    }

    private static void MarkMessage(JsonObject message)
    {
        switch (message["content"])
        {
            case JsonValue text when text.TryGetValue<string>(out var value) && value.Length > 0:
                message["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = value, ["cache_control"] = Ephemeral(hour: false) });
                break;
            case JsonArray parts:
                MarkLast(parts);
                break;
        }
    }

    private static int LastIndexOf(JsonArray messages, string role)
    {
        for (var index = messages.Count - 1; index >= 0; index--)
        {
            if (Role(messages[index]) == role)
            {
                return index;
            }
        }

        return -1;
    }

    private static string? Role(JsonNode? message) => (string?)message?["role"];

    private static JsonObject Ephemeral(bool hour) => hour
        ? new JsonObject { ["type"] = "ephemeral", ["ttl"] = "1h" }
        : new JsonObject { ["type"] = "ephemeral" };
}
