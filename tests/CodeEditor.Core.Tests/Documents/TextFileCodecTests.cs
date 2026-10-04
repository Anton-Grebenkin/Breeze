using System.Text;
using CodeEditor.Core.Documents;

namespace CodeEditor.Core.Tests.Documents;

public sealed class TextFileCodecTests
{
    [Fact]
    public void Utf8WithoutBom_IsDetectedByValidation()
    {
        var decoded = TextFileCodec.Decode(Encoding.UTF8.GetBytes("привет\nмир"))!.Value;

        Assert.Equal("привет\nмир", decoded.Text);
        Assert.Equal("UTF-8", decoded.Format.EncodingName);
        Assert.False(decoded.Format.HasByteOrderMark);
        Assert.Equal(LineEnding.Lf, decoded.Format.LineEnding);
    }

    [Fact]
    public void Utf8Bom_IsPreservedOnEncode()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("class A {}\r\n")];

        var decoded = TextFileCodec.Decode(bytes)!.Value;

        Assert.Equal("class A {}\r\n", decoded.Text);
        Assert.Equal("UTF-8 с BOM", decoded.Format.EncodingName);
        Assert.Equal(LineEnding.CrLf, decoded.Format.LineEnding);
        Assert.Equal(bytes, TextFileCodec.Encode(decoded.Text, decoded.Format));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x41, 0x00 }, "UTF-16 LE")]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x41 }, "UTF-16 BE")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00 }, "UTF-32")]
    public void UnicodeBoms_AreRecognized(byte[] bytes, string encodingName)
    {
        var decoded = TextFileCodec.Decode(bytes)!.Value;

        Assert.Equal("A", decoded.Text);
        Assert.Equal(encodingName, decoded.Format.EncodingName);
        Assert.Equal(bytes, TextFileCodec.Encode(decoded.Text, decoded.Format));
    }

    [Fact]
    public void InvalidUtf8_FallsBackToAnsiCodePage()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1251).GetBytes("Привет");

        var decoded = TextFileCodec.Decode(bytes)!.Value;

        Assert.NotEqual("UTF-8", decoded.Format.EncodingName);
        Assert.Equal(bytes, TextFileCodec.Encode(decoded.Text, decoded.Format));
    }

    [Fact]
    public void NulBytesWithoutBom_MeanBinary()
    {
        Assert.Null(TextFileCodec.Decode([0x4D, 0x5A, 0x90, 0x00, 0x03]));
    }

    [Fact]
    public void EmptyFile_IsUtf8WithWindowsLineEndings()
    {
        var decoded = TextFileCodec.Decode([])!.Value;

        Assert.Empty(decoded.Text);
        Assert.Equal(LineEnding.CrLf, decoded.Format.LineEnding);
    }
}
