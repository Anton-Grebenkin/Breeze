using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// Chat Completions event stream with a non-standard reasoning field (<see cref="ReasoningFieldPolicy"/>): <c>data:</c>
/// lines whose chunk has <c>reasoning</c> or <c>reasoning_details</c> are parsed and rebuilt, the rest pass as is.
/// Read line by line as data arrives, so first tokens are not delayed.
/// <para>
/// Empty text next to reasoning is removed: Grok and DeepSeek send <c>content: ""</c> in every chunk, the library made
/// each a separate message part, and reasoning chunks between them never merged, so one reply was stored as hundreds
/// of parts.
/// </para>
/// <para>A stray "{}" before call arguments is removed by <see cref="ToolArgumentsStreamFix"/>; a mid-stream failure is
/// turned into an error by <see cref="CompletionsErrorChunk"/>.</para>
/// </summary>
internal sealed class ReasoningFieldStream(Stream inner, bool keepReasoning) : Stream
{
    private const string DataPrefix = "data: ";
    private const string ReasoningMarker = "\"reasoning";
    private const byte LineFeed = (byte)'\n';

    // Only the client's JSON parser reads the line, so non-ASCII text need not be escaped as \uXXXX.
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly StreamReader _reader = new(inner, Encoding.UTF8);
    private readonly ToolArgumentsStreamFix _arguments = new();

    // The current line as UTF-8 plus "\n"; the buffer is reused and grows to the longest line.
    private byte[] _pending = [];
    private int _length;
    private int _offset;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Rewrites one stream line; lines without reasoning fields are not parsed.</summary>
    public static string RewriteLine(string line, bool keepReasoning)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!line.StartsWith(DataPrefix, StringComparison.Ordinal) || !line.Contains(ReasoningMarker, StringComparison.Ordinal))
        {
            return line;
        }

        if (JsonNode.Parse(line[DataPrefix.Length..]) is not JsonObject chunk || chunk["choices"] is not JsonArray choices)
        {
            return line;
        }

        foreach (var delta in choices.OfType<JsonObject>().Select(static choice => choice["delta"]).OfType<JsonObject>())
        {
            var reasoning = delta["reasoning"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            delta.Remove("reasoning");
            delta.Remove("reasoning_details");
            if (keepReasoning && reasoning is { Length: > 0 } && !delta.ContainsKey("reasoning_content"))
            {
                delta["reasoning_content"] = reasoning;
            }

            if (delta["content"] is JsonValue content && content.TryGetValue<string>(out var textContent) && textContent.Length == 0)
            {
                delta.Remove("content");
            }
        }

        return DataPrefix + chunk.ToJsonString(JsonOptions);
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_offset == _length && !Fill(_reader.ReadLine()))
        {
            return 0;
        }

        return Take(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset == _length && !Fill(await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)))
        {
            return 0;
        }

        return Take(buffer.Span);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reader.Dispose();
        }

        base.Dispose(disposing);
    }

    // ReadLine strips the line feed; it is added back so SSE event boundaries survive.
    private bool Fill(string? line)
    {
        if (line is null)
        {
            return false;
        }

        CompletionsErrorChunk.ThrowIfError(line);
        var text = _arguments.Rewrite(RewriteLine(line, keepReasoning));
        var size = Encoding.UTF8.GetByteCount(text) + 1;
        if (_pending.Length < size)
        {
            _pending = new byte[Math.Max(size, _pending.Length * 2)];
        }

        _length = Encoding.UTF8.GetBytes(text, _pending);
        _pending[_length++] = LineFeed;
        _offset = 0;
        return true;
    }

    private int Take(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _length - _offset);
        _pending.AsSpan(_offset, count).CopyTo(buffer);
        _offset += count;
        return count;
    }
}
