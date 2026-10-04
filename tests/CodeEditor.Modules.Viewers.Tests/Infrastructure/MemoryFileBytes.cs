using System.Collections.Concurrent;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.Tests.Infrastructure;

/// <summary>
/// In-memory file bytes for the dump. A huge file is defined by its length: the byte at an offset is
/// <c>offset % 251</c> and nothing is stored. Reads are recorded so the test sees which parts were read.
/// </summary>
internal sealed class MemoryFileBytes : IFileBytes
{
    private const int Modulus = 251;

    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _generated = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<(string Path, long Offset, int Length)> Reads { get; } = new();

    /// <summary>Reads fail with this error (file deleted, access denied).</summary>
    public Exception? Failure { get; set; }

    public static byte At(long offset) => (byte)(offset % Modulus);

    public void Add(string path, byte[] bytes) => _files[path] = bytes;

    /// <summary>A file of the given length without stored bytes: see <see cref="At"/>.</summary>
    public void AddGenerated(string path, long length) => _generated[path] = length;

    public long GetLength(string path)
    {
        ThrowIfFailing();
        return _files.TryGetValue(path, out var bytes) ? bytes.LongLength
            : _generated.TryGetValue(path, out var length) ? length
            : throw new FileNotFoundException("Нет файла.", path);
    }

    public int Read(string path, long offset, Span<byte> buffer)
    {
        ThrowIfFailing();
        Reads.Enqueue((path, offset, buffer.Length));
        var length = GetLength(path);
        var count = (int)Math.Clamp(length - offset, 0, buffer.Length);
        if (count == 0)
        {
            return 0;
        }

        if (_files.TryGetValue(path, out var bytes))
        {
            bytes.AsSpan((int)offset, count).CopyTo(buffer);
            return count;
        }

        for (var index = 0; index < count; index++)
        {
            buffer[index] = At(offset + index);
        }

        return count;
    }

    private void ThrowIfFailing()
    {
        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
