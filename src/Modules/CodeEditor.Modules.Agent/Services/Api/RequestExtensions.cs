using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Request fields beyond the OpenAI library, at the intersection of model and service (ADR 0021): the model says what it
/// needs (family, reasoning style), the service dialect says what it accepts. A field is sent only if the dialect allows it.
/// </summary>
internal static class RequestExtensions
{
    /// <param name="conversationKey">Conversation cache key (<see cref="PromptCacheKey"/>); <c>null</c> for none.</param>
    /// <returns>Body edits in order; empty means the body is sent as is.</returns>
    public static IReadOnlyList<Func<JsonNode, bool>> For(AgentOptions options, ServiceDialect dialect, Func<string>? conversationKey)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dialect);
        List<Func<JsonNode, bool>> edits = [];
        if (OpenAIChatClientFactory.UsesMessages(options))
        {
            // The Messages client builds the whole request itself and needs no OpenAI-format edits.
            return edits;
        }

        if (IsVendor(options, OpenAIChatClientFactory.AnthropicVendorPrefix))
        {
            AddClaude(edits, options, dialect);
        }

        if (OpenAIChatClientFactory.UsesResponses(options))
        {
            AddResponses(edits, dialect, conversationKey);
        }
        else if (dialect.LegacyMaxTokens)
        {
            edits.Add(CompletionsFields.UseLegacyMaxTokens);
        }

        if (IsVendor(options, OpenAIChatClientFactory.XaiVendorPrefix))
        {
            AddGrok(edits, options, dialect, conversationKey);
        }

        return edits;
    }

    /// <summary>Pins Grok to the server with its cache: the field is added here, the header by the factory.</summary>
    public static bool UsesGrokSessionKey(AgentOptions options, ServiceDialect dialect) =>
        dialect.GrokSessionKey && IsVendor(options, OpenAIChatClientFactory.XaiVendorPrefix);

    private static void AddClaude(List<Func<JsonNode, bool>> edits, AgentOptions options, ServiceDialect dialect)
    {
        if (dialect.CacheControlMarks)
        {
            edits.Add(AnthropicCache.Mark);
        }

        if (dialect.ThinkingField)
        {
            var thinking = ModelProfiles.For(options.Model).Thinking;
            edits.Add(body => AnthropicThinking.Enable(body, thinking));
        }

        if (!dialect.ClaudeReasoningEffort)
        {
            edits.Add(CompletionsFields.RemoveReasoningEffort);
        }
    }

    private static void AddGrok(List<Func<JsonNode, bool>> edits, AgentOptions options, ServiceDialect dialect, Func<string>? conversationKey)
    {
        if (conversationKey is not null && UsesGrokSessionKey(options, dialect))
        {
            edits.Add(body => CacheKeyField.Mark(body, CacheKeyField.SessionId, conversationKey()));
        }

        if (!dialect.GrokReasoningEffort)
        {
            edits.Add(CompletionsFields.RemoveReasoningEffort);
        }
    }

    private static void AddResponses(List<Func<JsonNode, bool>> edits, ServiceDialect dialect, Func<string>? conversationKey)
    {
        if (!dialect.EncryptedReasoning)
        {
            edits.Add(ResponsesReasoning.Strip);
        }

        if (dialect.ReplyPhases)
        {
            edits.Add(ResponsesPhases.Mark);
        }

        if (dialect.PromptCacheKey && conversationKey is not null)
        {
            edits.Add(body => CacheKeyField.Mark(body, CacheKeyField.PromptCacheKey, conversationKey()));
        }
    }

    private static bool IsVendor(AgentOptions options, string prefix) => options.Model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
