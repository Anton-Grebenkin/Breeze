namespace CodeEditor.Modules.Viewers.Services;

/// <summary>
/// File bytes on disk via <see cref="RandomAccess"/>: no shared stream position, so reads from different threads do not
/// interfere. The file is opened per read and lets others write and delete it.
/// </summary>
public sealed class DiskFileBytes : IFileBytes
{
    private const FileShare Sharing = FileShare.ReadWrite | FileShare.Delete;

    public long GetLength(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, Sharing);
        return RandomAccess.GetLength(handle);
    }

    public int Read(string path, long offset, Span<byte> buffer)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, Sharing, FileOptions.RandomAccess);
        var total = 0;
        while (total < buffer.Length)
        {
            var read = RandomAccess.Read(handle, buffer[total..], offset + total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
