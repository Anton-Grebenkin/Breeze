using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Anthropic;

/// <summary>
/// Claude reasoning: without the <c>thinking</c> parameter Claude does not reason even at high <c>reasoning_effort</c>.
/// ProxyAPI's OpenAI-compatible API forwards the parameter to Anthropic as is; reasoning text arrives in
/// <c>reasoning_content</c> and reaches the feed, but the blocks are not sent back in history: the library cannot do it
/// and Anthropic accepts history without them (ADR 0010). Applied by <see cref="JsonBodyPolicy"/>; the Anthropic Messages
/// request takes the same parameter from <see cref="Config"/>.
/// </summary>
internal static class AnthropicThinking
{
    /// <summary>The smallest budget Anthropic accepts.</summary>
    private const int MinBudgetTokens = 1024;

    private const int LowBudgetTokens = 2048;
    private const int DefaultBudgetTokens = 4096;
    private const int HighBudgetTokens = 8192;
    private const int ExtraHighBudgetTokens = 16384;

    /// <summary>Adds <c>thinking</c> to a Chat Completions request unless reasoning is turned off in settings.</summary>
    public static bool Enable(JsonNode body, ClaudeThinking mode)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body is not JsonObject request || request["messages"] is null || request.ContainsKey("thinking")
            || Config(mode, (string?)request["reasoning_effort"], MaxTokens(request)) is not { } thinking)
        {
            return false;
        }

        request["thinking"] = thinking;
        return true;
    }

    /// <param name="effort">Reasoning effort from settings (<c>low</c> … <c>xhigh</c>, <c>none</c>); <c>null</c> for the default.</param>
    /// <param name="maxTokens">Response limit: a Claude 4 budget gets at most half of it.</param>
    /// <returns>The <c>thinking</c> value; <c>null</c> if reasoning is off or the budget is below the minimum.</returns>
    public static JsonObject? Config(ClaudeThinking mode, string? effort, int? maxTokens)
    {
        if (mode == ClaudeThinking.None || effort == "none")
        {
            return null;
        }

        if (mode == ClaudeThinking.Adaptive)
        {
            return new JsonObject { ["type"] = "adaptive", ["display"] = "summarized" };
        }

        var budget = Budget(effort, maxTokens);
        return budget < MinBudgetTokens ? null : new JsonObject { ["type"] = "enabled", ["budget_tokens"] = budget };
    }

    // The budget grows with the effort, but half of the response limit is left for the answer itself.
    private static int Budget(string? effort, int? maxTokens)
    {
        var budget = effort switch
        {
            "low" or "minimal" => LowBudgetTokens,
            "high" => HighBudgetTokens,
            "xhigh" => ExtraHighBudgetTokens,
            _ => DefaultBudgetTokens,
        };
        return maxTokens is { } max ? Math.Min(budget, max / 2) : budget;
    }

    private static int? MaxTokens(JsonObject request) =>
        (request["max_completion_tokens"] ?? request["max_tokens"]) is JsonValue value && value.TryGetValue<int>(out var max) ? max : null;
}
