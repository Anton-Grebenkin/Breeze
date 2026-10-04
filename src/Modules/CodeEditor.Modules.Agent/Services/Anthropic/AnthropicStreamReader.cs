using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Anthropic;

/// <summary>
/// Turns an Anthropic Messages event stream into chat updates. Text and reasoning stream as chunks arrive; the
/// reasoning signature comes as a separate empty reasoning at the block end (when the response is assembled it merges
/// with the block text, so the whole block goes back in history, <see cref="AnthropicMessages"/>); a call comes whole
/// at the block end once its argument chunks are collected. Usage and finish reason come in <c>message_delta</c>; a
/// stream error becomes <see cref="ModelStreamException"/>. State covers one response.
/// </summary>
internal sealed class AnthropicStreamReader(string model)
{
    private readonly Dictionary<int, Block> _blocks = [];
    private readonly DateTimeOffset _created = DateTimeOffset.UtcNow;
    private string? _id;
    private long _input;
    private long _cacheRead;
    private long _cacheWrite;

    public async IAsyncEnumerable<ChatResponseUpdate> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
        {
            // Skip proxy service lines that are not JSON: Provod sends "[DONE]" after message_stop, as in Chat Completions.
            if (!item.Data.AsSpan().TrimStart().StartsWith('{'))
            {
                continue;
            }

            using var document = JsonDocument.Parse(item.Data);
            if (Text(document.RootElement, "type") == "message_stop")
            {
                yield break;
            }

            if (Handle(document.RootElement) is { } update)
            {
                yield return update;
            }
        }
    }

    private ChatResponseUpdate? Handle(JsonElement data) => Text(data, "type") switch
    {
        "message_start" => Start(data.GetProperty("message")),
        "content_block_start" => BlockStart(data),
        "content_block_delta" => BlockDelta(data),
        "content_block_stop" => BlockStop(data),
        "message_delta" => MessageDelta(data),
        "error" => throw new ModelStreamException(ErrorText(data)),
        _ => null,
    };

    private ChatResponseUpdate? Start(JsonElement message)
    {
        _id = Text(message, "id");
        if (message.TryGetProperty("usage", out var usage))
        {
            ReadInput(usage);
        }

        return null;
    }

    private ChatResponseUpdate? BlockStart(JsonElement data)
    {
        var block = data.GetProperty("content_block");
        var started = new Block(Text(block, "type") ?? string.Empty, Text(block, "id"), Text(block, "name"));
        _blocks[data.GetProperty("index").GetInt32()] = started;
        return started.Type switch
        {
            "redacted_thinking" => Update(new TextReasoningContent(string.Empty) { ProtectedData = AnthropicMessages.RedactedPrefix + Text(block, "data") }),
            "text" when Text(block, "text") is { Length: > 0 } text => Update(new TextContent(text)),
            _ => null,
        };
    }

    private ChatResponseUpdate? BlockDelta(JsonElement data)
    {
        var block = _blocks.GetValueOrDefault(data.GetProperty("index").GetInt32());
        var delta = data.GetProperty("delta");
        switch (Text(delta, "type"))
        {
            case "text_delta":
                return Update(new TextContent(Text(delta, "text")));
            case "thinking_delta":
                return Update(new TextReasoningContent(Text(delta, "thinking")));
            case "signature_delta":
                block?.Signature.Append(Text(delta, "signature"));
                return null;
            case "input_json_delta":
                block?.Arguments.Append(Text(delta, "partial_json"));
                return null;
            default:
                return null;
        }
    }

    private ChatResponseUpdate? BlockStop(JsonElement data)
    {
        if (!_blocks.Remove(data.GetProperty("index").GetInt32(), out var block))
        {
            return null;
        }

        return block.Type switch
        {
            "thinking" when block.Signature.Length > 0 => Update(new TextReasoningContent(string.Empty) { ProtectedData = block.Signature.ToString() }),
            "tool_use" => Update(FunctionCallContent.CreateFromParsedArguments(
                block.Arguments.Length > 0 ? block.Arguments.ToString() : "{}",
                block.Id ?? string.Empty,
                block.Name ?? string.Empty,
                static json => JsonSerializer.Deserialize<Dictionary<string, object?>>(json, AIJsonUtilities.DefaultOptions))),
            _ => null,
        };
    }

    private ChatResponseUpdate MessageDelta(JsonElement data)
    {
        long output = 0;
        if (data.TryGetProperty("usage", out var usage))
        {
            ReadInput(usage);
            output = Number(usage, "output_tokens");
        }

        var input = _input + _cacheRead + _cacheWrite;
        var update = Update(new UsageContent(new UsageDetails
        {
            InputTokenCount = input,
            CachedInputTokenCount = _cacheRead,
            OutputTokenCount = output,
            TotalTokenCount = input + output,
        }));
        update.FinishReason = data.TryGetProperty("delta", out var delta) ? FinishReason(Text(delta, "stop_reason")) : null;
        return update;
    }

    // Input counters arrive at the start and, in newer API versions, again at the end: the last value wins.
    private void ReadInput(JsonElement usage)
    {
        _input = Number(usage, "input_tokens", _input);
        _cacheRead = Number(usage, "cache_read_input_tokens", _cacheRead);
        _cacheWrite = Number(usage, "cache_creation_input_tokens", _cacheWrite);
    }

    private ChatResponseUpdate Update(AIContent content) => new(ChatRole.Assistant, [content])
    {
        MessageId = _id,
        ResponseId = _id,
        ModelId = model,
        CreatedAt = _created,
    };

    private static ChatFinishReason? FinishReason(string? reason) => reason switch
    {
        null => null,
        "end_turn" or "stop_sequence" or "pause_turn" => ChatFinishReason.Stop,
        "tool_use" => ChatFinishReason.ToolCalls,
        "max_tokens" or "model_context_window_exceeded" => ChatFinishReason.Length,
        "refusal" => ChatFinishReason.ContentFilter,
        _ => new ChatFinishReason(reason),
    };

    private static string ErrorText(JsonElement data) =>
        data.TryGetProperty("error", out var error) ? $"{Text(error, "message")} ({Text(error, "type")})" : data.GetRawText();

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long Number(JsonElement element, string name, long fallback = 0) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : fallback;

    /// <summary>An open response block: the reasoning signature and call arguments are assembled from chunks.</summary>
    private sealed class Block(string type, string? id, string? name)
    {
        public string Type { get; } = type;

        public string? Id { get; } = id;

        public string? Name { get; } = name;

        public StringBuilder Signature { get; } = new();

        public StringBuilder Arguments { get; } = new();
    }
}
