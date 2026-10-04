using CodeEditor.Modules.Viewers.Hex;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>Offset input: "0x", "h" or letters mean hex; plain digits give both readings, hex first.</summary>
public sealed class OffsetInputTests
{
    [Theory]
    [InlineData("0x1F40", 0x1F40)]
    [InlineData("0X1f40", 0x1F40)]
    [InlineData("1F40h", 0x1F40)]
    [InlineData("ff", 0xFF)]
    [InlineData("  0x10  ", 0x10)]
    public void HexadecimalInput_HasOneReading(string text, long offset) =>
        Assert.Equal([new OffsetCandidate(offset, IsHexadecimal: true)], OffsetInput.Parse(text));

    [Fact]
    public void Digits_ReadBothWays_HexadecimalFirst() =>
        Assert.Equal([new OffsetCandidate(0x8000, IsHexadecimal: true), new OffsetCandidate(8000, IsHexadecimal: false)], OffsetInput.Parse("8000"));

    [Fact]
    public void SingleDigit_ReadsTheSameBothWays() => Assert.Equal([new OffsetCandidate(7, IsHexadecimal: false)], OffsetInput.Parse("7"));

    [Theory]
    [InlineData("")]
    [InlineData("0x")]
    [InlineData("-5")]
    [InlineData("12 34")]
    [InlineData("смещение")]
    [InlineData("0xFFFFFFFFFFFFFFFF")]
    public void Garbage_HasNoReading(string text) => Assert.Empty(OffsetInput.Parse(text));
}
