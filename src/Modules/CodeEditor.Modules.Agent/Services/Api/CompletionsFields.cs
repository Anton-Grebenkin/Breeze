using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Standard Chat Completions fields that the service rejects for some models (<see cref="ServiceDialect"/>).
/// Applied by <see cref="JsonBodyPolicy"/>.
/// </summary>
internal static class CompletionsFields
{
    private const string MaxCompletionTokens = "max_completion_tokens";
    private const string MaxTokens = "max_tokens";
    private const string ReasoningEffort = "reasoning_effort";

    /// <summary>Sends the response limit as legacy <c>max_tokens</c>: all Provod models accept it, not all accept the new one.</summary>
    public static bool UseLegacyMaxTokens(JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body is not JsonObject fields || !fields.Remove(MaxCompletionTokens, out var limit))
        {
            return false;
        }

        fields[MaxTokens] = limit;
        return true;
    }

    /// <summary>
    /// Drops the reasoning effort: with it Provod routes Claude through a path that corrupts call arguments, and Grok 4
    /// rejects it outright.
    /// </summary>
    public static bool RemoveReasoningEffort(JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body is JsonObject fields && fields.Remove(ReasoningEffort);
    }
}
