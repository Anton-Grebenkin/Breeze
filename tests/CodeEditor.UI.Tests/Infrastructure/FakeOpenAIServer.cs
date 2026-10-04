using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Fake model for end-to-end agent tests: a local HTTP server speaking OpenAI Chat Completions (SSE stream). The app
/// reaches it via <c>agent.endpoint</c> with the real OpenAI client and Agent Framework, no network and no test hooks
/// in the product. Responses are queued in request order.
/// </summary>
internal sealed class FakeOpenAIServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentQueue<Func<string>> _responses = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public FakeOpenAIServer()
    {
        _listener.Start();
        _loop = Task.Run(AcceptLoopAsync);
    }

    public string Endpoint => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1";

    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>What <c>GET /models</c> returns.</summary>
    public IReadOnlyList<string> Models { get; set; } = ["test/model"];

    public FakeOpenAIServer Reply(string text)
    {
        _responses.Enqueue(() => Chunk(new { role = "assistant", content = text }, null) + Chunk(new { }, "stop"));
        return this;
    }

    /// <summary>A reply with token usage in the last chunk, as OpenAI does with <c>stream_options.include_usage</c>.</summary>
    public FakeOpenAIServer ReplyWithUsage(string text, int promptTokens, int completionTokens)
    {
        var usage = new
        {
            id = "chatcmpl-test",
            @object = "chat.completion.chunk",
            created = 0,
            model = "test/model",
            choices = Array.Empty<object>(),
            usage = new { prompt_tokens = promptTokens, completion_tokens = completionTokens, total_tokens = promptTokens + completionTokens },
        };
        _responses.Enqueue(() => Chunk(new { role = "assistant", content = text }, null) + Chunk(new { }, "stop") + $"data: {JsonSerializer.Serialize(usage)}\n\n");
        return this;
    }

    public FakeOpenAIServer CallTool(string name, object arguments)
    {
        var call = new { index = 0, id = "call_" + Guid.NewGuid().ToString("N")[..8], type = "function", function = new { name, arguments = JsonSerializer.Serialize(arguments) } };
        _responses.Enqueue(() => Chunk(new { role = "assistant", tool_calls = new[] { call } }, null) + Chunk(new { }, "tool_calls"));
        return this;
    }

    /// <summary>A sentence about what the model is doing plus a tool call in one reply, as some models do.</summary>
    public FakeOpenAIServer SayAndCallTool(string text, string name, object arguments)
    {
        var call = new { index = 0, id = "call_" + Guid.NewGuid().ToString("N")[..8], type = "function", function = new { name, arguments = JsonSerializer.Serialize(arguments) } };
        _responses.Enqueue(() => Chunk(new { role = "assistant", content = text }, null) + Chunk(new { tool_calls = new[] { call } }, null) + Chunk(new { }, "tool_calls"));
        return this;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Stopping interrupted the pending accept.
        }

        _stop.Dispose();
    }

    private static string Chunk(object delta, string? finishReason)
    {
        var chunk = new
        {
            id = "chatcmpl-test",
            @object = "chat.completion.chunk",
            created = 0,
            model = "test/model",
            choices = new[] { new { index = 0, delta, finish_reason = finishReason } },
        };
        return $"data: {JsonSerializer.Serialize(chunk)}\n\n";
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            await HandleAsync(client);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var (requestLine, requestBody) = await ReadRequestAsync(stream);
        var bytes = requestLine.StartsWith("GET ", StringComparison.Ordinal) && requestLine.Contains("/models", StringComparison.Ordinal)
            ? ModelsResponse()
            : ChatResponse(requestBody);
        await stream.WriteAsync(bytes, _stop.Token);
        await stream.FlushAsync(_stop.Token);
    }

    private byte[] ChatResponse(string requestBody)
    {
        Requests.Enqueue(requestBody);
        var body = _responses.TryDequeue(out var response) ? response() : Chunk(new { role = "assistant", content = "(нет заготовленного ответа)" }, "stop");
        return Encoding.UTF8.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n" + body + "data: [DONE]\n\n");
    }

    // Model list like OpenAI's GET /v1/models, read by the model manager.
    private byte[] ModelsResponse()
    {
        var json = JsonSerializer.Serialize(new { @object = "list", data = Models.Select(id => new { id, @object = "model", created = 0, owned_by = "test" }) });
        return Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(json)}\r\nConnection: close\r\n\r\n{json}");
    }

    // Headers up to the blank line, then the body by Content-Length.
    private async Task<(string RequestLine, string Body)> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new List<byte>();
        var chunk = new byte[8192];
        int headerEnd;
        while ((headerEnd = IndexOfHeaderEnd(buffer)) < 0)
        {
            var read = await stream.ReadAsync(chunk, _stop.Token);
            if (read == 0)
            {
                return (string.Empty, string.Empty);
            }

            buffer.AddRange(chunk.AsSpan(0, read));
        }

        var headers = Encoding.ASCII.GetString([.. buffer.Take(headerEnd)]);
        var length = headers.Split("\r\n").Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(parts => int.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault();
        while (buffer.Count - headerEnd - 4 < length)
        {
            var read = await stream.ReadAsync(chunk, _stop.Token);
            if (read == 0)
            {
                break;
            }

            buffer.AddRange(chunk.AsSpan(0, read));
        }

        return (headers.Split("\r\n")[0], Encoding.UTF8.GetString([.. buffer.Skip(headerEnd + 4)]));
    }

    private static int IndexOfHeaderEnd(List<byte> buffer)
    {
        for (var i = 3; i < buffer.Count; i++)
        {
            if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
            {
                return i - 3;
            }
        }

        return -1;
    }
}
