using CodeEditor.Modules.Viewers.Hex;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Dump lines: offset, hex bytes with a gap in the middle, ASCII characters; a short line doesn't shift the columns.
/// </summary>
public sealed class HexFormatterTests
{
    private static readonly byte[] Header = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x41, 0x7E];

    [Fact]
    public void FullRow_HasSixteenPairs_WithAGapInTheMiddle() =>
        Assert.Equal("4D 5A 90 00 03 00 00 00  04 00 00 00 FF FF 41 7E", HexFormatter.Hex(Header).TrimEnd());

    [Fact]
    public void Text_ShowsPrintableAscii_AndDotsForTheRest() => Assert.Equal("MZ............A~", HexFormatter.Text(Header));

    [Fact]
    public void ShortRow_IsPaddedToTheFullWidth()
    {
        var hex = HexFormatter.Hex([0x41, 0x42, 0x43]);

        Assert.Equal(HexFormatter.HexWidth, hex.Length);
        Assert.StartsWith("41 42 43 ", hex, StringComparison.Ordinal);
        Assert.Equal("ABC", HexFormatter.Text([0x41, 0x42, 0x43]));
    }

    [Fact]
    public void Header_NumbersTheBytes() =>
        Assert.Equal("00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F", HexFormatter.BytesHeader.TrimEnd());

    [Fact]
    public void Columns_PointAtEachByte()
    {
        var hex = HexFormatter.Hex(Header);

        Assert.Equal("4D", hex.Substring(HexFormatter.HexColumn(0), 2));
        Assert.Equal("00", hex.Substring(HexFormatter.HexColumn(7), 2));
        Assert.Equal("04", hex.Substring(HexFormatter.HexColumn(8), 2));
        Assert.Equal("7E", hex.Substring(HexFormatter.HexColumn(15), 2));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(4096, 8)]
    [InlineData(0x1_0000_0000, 8)]
    [InlineData(0x1_0000_0001, 9)]
    [InlineData(long.MaxValue, 16)]
    public void OffsetDigits_GrowWithTheFile(long length, int digits) => Assert.Equal(digits, HexFormatter.OffsetDigits(length));

    [Fact]
    public void Offset_IsUppercaseHex_WithLeadingZeros() => Assert.Equal("00001F40", HexFormatter.Offset(0x1F40, 8));

    [Fact]
    public void MoreThanARow_IsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => HexFormatter.Hex(new byte[17]));
}
