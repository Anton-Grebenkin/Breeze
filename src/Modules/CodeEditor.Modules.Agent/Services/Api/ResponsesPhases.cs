using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Restores the <c>phase</c> field of GPT replies in Responses API history. GPT-5+ writes a preamble
/// (<c>commentary</c>) before tool calls and the turn result as <c>final_answer</c>; the library drops the field, and on
/// replay the model may take a preamble for the end of work. The phase follows from the next step: a tool call means
/// preamble, a user message or the end of history means final answer. One backward pass, O(n).
/// Applied by <see cref="JsonBodyPolicy"/>.
/// </summary>
internal static class ResponsesPhases
{
    private const string Commentary = "commentary";
    private const string FinalAnswer = "final_answer";

    public static bool Mark(JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body["input"] is not JsonArray items)
        {
            return false;
        }

        var changed = false;
        var phase = FinalAnswer;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is not JsonObject item)
            {
                continue;
            }

            if ((string?)item["type"] == "function_call")
            {
                phase = Commentary;
            }
            else if (item["role"] is { } role && (string?)item["type"] is null or "message")
            {
                changed |= Mark(item, (string?)role, ref phase);
            }
        }

        return changed;
    }

    // Consecutive replies share a phase; a user message closes the previous turn.
    private static bool Mark(JsonObject message, string? role, ref string phase)
    {
        if (role != "assistant")
        {
            phase = FinalAnswer;
            return false;
        }

        if (message.ContainsKey("phase"))
        {
            return false;
        }

        message["phase"] = phase;
        return true;
    }
}
