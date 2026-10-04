using BenchmarkDotNet.Attributes;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Testing;

namespace CodeEditor.Benchmarks;

/// <summary>
/// Hex viewer (ADR 0037): a dump row is formatted when the list shows it, so scrolling formats a screen of rows per
/// frame. The file page is already cached; only UI thread work is measured.
/// </summary>
[MemoryDiagnoser]
public class HexDumpBenchmarks
{
    private const string Path = @"C:\bench\disk.img";
    private const int ScreenRows = 50;

    private readonly byte[] _row = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x41, 0x7E];
    private HexPages _pages = null!;
    private HexRows _rows = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pages = new HexPages(Path, new GeneratedBytes(), new InlineUiDispatcher());
        _rows = new HexRows(_pages, "Offset");
        _rows.Reload(GeneratedBytes.Length);
        _pages.LoadAsync(0).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _pages.Dispose();

    [Benchmark]
    public int FormatRow() => HexFormatter.Hex(_row).Length + HexFormatter.Text(_row).Length;

    /// <summary>A screen of rows from a loaded page, like one scroll frame.</summary>
    [Benchmark]
    public int ScreenOfRows()
    {
        var total = 0;
        for (var index = 0; index < ScreenRows; index++)
        {
            total += _rows[index].Hex.Length;
        }

        return total;
    }

    /// <summary>A 4 GB file that stores no bytes.</summary>
    private sealed class GeneratedBytes : IFileBytes
    {
        public const long Length = 4L * 1024 * 1024 * 1024;

        public long GetLength(string path) => Length;

        public int Read(string path, long offset, Span<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, Length - offset);
            for (var index = 0; index < count; index++)
            {
                buffer[index] = (byte)(offset + index);
            }

            return count;
        }
    }
}
