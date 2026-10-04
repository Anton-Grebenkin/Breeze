using System.Text;
using System.Text.Json;

namespace CodeEditor.Shell.Instances;

/// <summary>The pipe protocol between launches: the request as one JSON line, then <c>ok</c> back.</summary>
internal static class InstanceProtocol
{
    private const string Acknowledgement = "ok";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static Task WriteRequestAsync(Stream pipe, InstanceRequest request, CancellationToken cancellationToken) =>
        WriteLineAsync(pipe, JsonSerializer.Serialize(request, InstanceJsonContext.Default.InstanceRequest), cancellationToken);

    public static async Task<InstanceRequest> ReadRequestAsync(Stream pipe, CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(pipe, cancellationToken).ConfigureAwait(false) ?? throw new IOException("The request is empty");
        return JsonSerializer.Deserialize(line, InstanceJsonContext.Default.InstanceRequest) ?? throw new JsonException("The request is null");
    }

    public static Task WriteAcknowledgementAsync(Stream pipe, CancellationToken cancellationToken) =>
        WriteLineAsync(pipe, Acknowledgement, cancellationToken);

    public static async Task<bool> ReadAcknowledgementAsync(Stream pipe, CancellationToken cancellationToken) =>
        await ReadLineAsync(pipe, cancellationToken).ConfigureAwait(false) == Acknowledgement;

    private static async Task WriteLineAsync(Stream pipe, string line, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(pipe, Utf8, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadLineAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, Utf8, leaveOpen: true);
        return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }
}
