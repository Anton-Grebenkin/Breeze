using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Anthropic;

/// <summary>
/// Builds the Anthropic Messages request body from chat history and options: blocks via <see cref="AnthropicMessages"/>,
/// reasoning via <see cref="AnthropicThinking"/>, cache marks via <see cref="AnthropicCache"/>. Claude 5 reasoning depth
/// goes in <c>output_config.effort</c>: "adaptive" mode decides how much to think, the effort how thoroughly.
/// </summary>
internal static class AnthropicRequest
{
    /// <summary>Messages requires a response limit; used when neither the chat nor the model profile sets one.</summary>
    private const int DefaultMaxTokens = 32_000;

    /// <summary>The warm-up answer is not needed: only reading the history is paid for.</summary>
    private const int WarmUpMaxTokens = 1;

    public static JsonObject Build(string model, IEnumerable<ChatMessage> messages, ChatOptions? options) =>
        Build(model, messages, options, warmUp: false);

    /// <summary>
    /// Cache warm-up request (<see cref="CacheWarmupChatClient"/>): the same history and options as the conversation,
    /// otherwise cache entries would not match (reasoning, effort and tool choice are part of the cache key), but with
    /// one-hour marks and a one-token answer. Budgeted reasoning cannot be warmed this way: the response limit must
    /// exceed the budget.
    /// </summary>
    public static JsonObject BuildWarmUp(string model, IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var body = Build(model, messages, options, warmUp: true);
        body["max_tokens"] = WarmUpMaxTokens;
        return body;
    }

    private static JsonObject Build(string model, IEnumerable<ChatMessage> messages, ChatOptions? options, bool warmUp)
    {
        ArgumentNullException.ThrowIfNull(model);
        var profile = ModelProfiles.For(model);
        var (system, turns) = AnthropicMessages.Convert(messages);

        // Agent instructions come as a chat option, not a system message; they go first in the system prompt.
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            system.Insert(0, new JsonObject { ["type"] = "text", ["text"] = options.Instructions });
        }

        var maxTokens = options?.MaxOutputTokens ?? profile.DefaultMaxOutputTokens ?? DefaultMaxTokens;
        var body = new JsonObject { ["model"] = model, ["max_tokens"] = maxTokens, ["stream"] = true };
        if (warmUp)
        {
            AnthropicCache.MarkWarmUp(system, turns);
        }
        else
        {
            AnthropicCache.MarkConversation(system, turns);
        }

        if (system.Count > 0)
        {
            body["system"] = system;
        }

        body["messages"] = turns;
        AddTools(body, options);
        var effort = Effort(options?.Reasoning?.Effort);
        var thinking = AnthropicThinking.Config(profile.Thinking, effort, maxTokens);
        if (thinking is not null)
        {
            body["thinking"] = thinking;
            if (profile.Thinking == ClaudeThinking.Adaptive && effort is not null)
            {
                body["output_config"] = new JsonObject { ["effort"] = effort };
            }
        }
        else if (options?.Temperature is { } temperature && profile.AcceptsTemperature)
        {
            body["temperature"] = temperature;
        }

        if (options?.StopSequences is { Count: > 0 } stops)
        {
            body["stop_sequences"] = new JsonArray([.. stops.Select(stop => (JsonNode)stop)]);
        }

        return body;
    }

    private static void AddTools(JsonObject body, ChatOptions? options)
    {
        var tools = options?.Tools?.OfType<AIFunctionDeclaration>().ToList() ?? [];
        if (tools.Count == 0 || options?.ToolMode is NoneChatToolMode)
        {
            return;
        }

        body["tools"] = new JsonArray([.. tools.Select(tool => (JsonNode)new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["input_schema"] = JsonNode.Parse(tool.JsonSchema.GetRawText()),
        })]);
        if (options?.ToolMode is RequiredChatToolMode required)
        {
            body["tool_choice"] = required.RequiredFunctionName is { } name
                ? new JsonObject { ["type"] = "tool", ["name"] = name }
                : new JsonObject { ["type"] = "any" };
        }
    }

    // Effort in Anthropic's names; the default sends no field and lets the model decide.
    private static string? Effort(ReasoningEffort? effort) => effort switch
    {
        ReasoningEffort.None => "none",
        ReasoningEffort.Low => "low",
        ReasoningEffort.Medium => "medium",
        ReasoningEffort.High => "high",
        ReasoningEffort.ExtraHigh => "xhigh",
        _ => null,
    };
}
