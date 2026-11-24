using System.Text;
using CodeEditor.Core.Processes;

namespace CodeEditor.Core.Tests;

/// <summary>Process output as bytes: lines split before decoding, UTF-8 first, then the console code page.</summary>
public sealed class ProcessOutputReaderTests
{
    private static readonly Encoding Oem866 = CodePage(866);

    [Fact]
    public void Decode_ValidUtf8_IsUtf8() =>
        Assert.Equal("строка:1 знак:1", ProcessOutputReader.Decode(Encoding.UTF8.GetBytes("строка:1 знак:1\r"), Oem866));

    // A PowerShell error printed before the script switched the console to UTF-8.
    [Fact]
    public void Decode_ConsoleCodePage_IsReadable() =>
        Assert.Equal("Этот сценарий содержит вредоносное содержимое", ProcessOutputReader.Decode(Oem866.GetBytes("Этот сценарий содержит вредоносное содержимое"), Oem866));

    [Fact]
    public void Decode_DropsTheByteOrderMark() =>
        Assert.Equal("ok", ProcessOutputReader.Decode([0xEF, 0xBB, 0xBF, (byte)'o', (byte)'k'], Oem866));

    // Lines arrive in arbitrary chunks: a multi-byte character or a line may be split between reads.
    [Fact]
    public async Task ReadLines_SplitsOnLineFeeds_AcrossChunks()
    {
        var bytes = Encoding.UTF8.GetBytes("первая\r\nвторая строка\nбез перевода строки");
        var lines = new List<string>();

        await ProcessOutputReader.ReadLinesAsync(new ChunkedStream(bytes, chunk: 5), lines.Add);

        Assert.Equal(["первая", "вторая строка", "без перевода строки"], lines);
    }

    [Fact]
    public async Task ReadLines_EmptyLines_AreKept()
    {
        var lines = new List<string>();

        await ProcessOutputReader.ReadLinesAsync(new MemoryStream(Encoding.UTF8.GetBytes("a\n\nb\n")), lines.Add);

        Assert.Equal(["a", string.Empty, "b"], lines);
    }

    private static Encoding CodePage(int codePage)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(codePage);
    }

    /// <summary>Returns at most <paramref name="chunk"/> bytes per read, like a pipe.</summary>
    private sealed class ChunkedStream(byte[] bytes, int chunk) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(chunk, buffer.Length)], cancellationToken);
    }
}
