namespace CodeEditor.Modules.Viewers.Services;

/// <summary>
/// Reads file bytes at any position, for the hex view of multi-gigabyte files. The file is not kept open between reads:
/// a build or another program may overwrite it.
/// </summary>
public interface IFileBytes
{
    /// <exception cref="IOException">The file is missing or unreadable.</exception>
    long GetLength(string path);

    /// <summary>Reads bytes from <paramref name="offset"/>; fewer than the buffer holds at the end of the file. Thread-safe.</summary>
    /// <returns>The number of bytes read.</returns>
    /// <exception cref="IOException">The file is missing or unreadable.</exception>
    int Read(string path, long offset, Span<byte> buffer);
}
