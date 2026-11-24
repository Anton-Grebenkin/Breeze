using System.Text;
using System.Text.Json;

namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>What a probe request came back with: the model's message, or why there is none.</summary>
/// <param name="Error">The service's refusal; <c>null</c> for an answer.</param>
/// <param name="Reasoning">The reasoning the service sent in a field of its own; empty for none.</param>
internal sealed record ModelProbeReply(int Status, string? Error, string Content, string Reasoning, IReadOnlyList<ModelProbeCall> Calls)
{
    private const int MaxErrorLength = 200;

    private static readonly string[] ReasoningFields = ["reasoning_content", "reasoning"];

    public bool IsAnswer => Error is null;

    public static ModelProbeReply Refused(int status, string text) =>
        new(status, text.Length > MaxErrorLength ? text[..MaxErrorLength] + "…" : text, string.Empty, string.Empty, []);

    /// <summary>The first choice of a Chat Completions answer; an answer without one is empty, not an error.</summary>
    public static ModelProbeReply Parse(int status, JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return new ModelProbeReply(status, null, string.Empty, string.Empty, []);
        }

        var reasoning = ReasoningFields.Select(field => Text(message, field)).FirstOrDefault(static text => text.Length > 0) ?? string.Empty;
        return new ModelProbeReply(status, null, ContentOf(message), reasoning, CallsOf(message));
    }

    /// <summary>The text of a refusal: <c>error.message</c>, <c>error</c>, <c>message</c> or <c>detail</c>, whichever the service uses.</summary>
    public static string ErrorOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (root.TryGetProperty("error", out var error))
        {
            return error.ValueKind == JsonValueKind.Object ? Text(error, "message") : error.ValueKind == JsonValueKind.String ? error.GetString() ?? string.Empty : string.Empty;
        }

        return Text(root, "message") is { Length: > 0 } message ? message : Text(root, "detail");
    }

    // Content is a string, or parts of which the text ones count.
    private static string ContentOf(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        var text = new StringBuilder();
        if (content.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in content.EnumerateArray().Where(static part => part.ValueKind == JsonValueKind.Object))
            {
                text.Append(Text(part, "text"));
            }
        }

        return text.ToString();
    }

    private static List<ModelProbeCall> CallsOf(JsonElement message)
    {
        List<ModelProbeCall> calls = [];
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                if (call.ValueKind == JsonValueKind.Object && call.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.Object)
                {
                    calls.Add(new ModelProbeCall(Text(function, "name"), Text(function, "arguments")));
                }
            }
        }

        return calls;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
