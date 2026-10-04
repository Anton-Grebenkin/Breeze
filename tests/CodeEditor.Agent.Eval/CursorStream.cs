using System.Text.Json;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Cursor turn metrics from <c>--output-format stream-json</c>: model requests are distinct <c>model_call_id</c>s plus
/// the final answer, tool calls are <c>tool_call/started</c> events, usage is the <c>usage</c> field of the final
/// <c>result</c> event (fresh input, cache reads and writes, output).
/// </summary>
internal sealed class CursorStream
{
    private readonly HashSet<string> _modelCalls = new(StringComparer.Ordinal);

    public int ToolCalls { get; private set; }

    /// <summary>The final event arrived: the turn ended on its own rather than being cut off.</summary>
    public bool Finished { get; private set; }

    public int Requests => _modelCalls.Count + (Finished ? 1 : 0);

    /// <summary>All input, including cache reads and writes.</summary>
    public long InputTokens { get; private set; }

    public long CachedInputTokens { get; private set; }

    public long OutputTokens { get; private set; }

    /// <summary>The model's last message, i.e. the turn's answer.</summary>
    public string Answer { get; private set; } = string.Empty;

    public string? Error { get; private set; }

    public void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
        {
            return;
        }

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.TryGetProperty("model_call_id", out var call) && call.GetString() is { } id)
        {
            _modelCalls.Add(id);
        }

        switch (Text(root, "type"))
        {
            case "tool_call" when Text(root, "subtype") == "started":
                ToolCalls++;
                break;
            case "assistant":
                Answer = string.Concat(root.GetProperty("message").GetProperty("content").EnumerateArray()
                    .Where(part => Text(part, "type") == "text").Select(part => Text(part, "text")));
                break;
            case "result":
                Finish(root);
                break;
        }
    }

    private void Finish(JsonElement result)
    {
        Finished = true;
        if (result.TryGetProperty("is_error", out var isError) && isError.GetBoolean())
        {
            Error = Text(result, "result") ?? "ошибка Cursor";
        }

        if (!result.TryGetProperty("usage", out var usage))
        {
            return;
        }

        CachedInputTokens = Number(usage, "cacheReadTokens");
        InputTokens = Number(usage, "inputTokens") + CachedInputTokens + Number(usage, "cacheWriteTokens");
        OutputTokens = Number(usage, "outputTokens");
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;
}
