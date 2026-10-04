using CodeEditor.Modules.Viewers.Formats;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Format detection: viewer kind by extension, image format by signature, EXIF orientation and the decode size of
/// large images.
/// </summary>
public sealed class FormatTests
{
    [Theory]
    [InlineData("photo.JPG", ViewerKind.Image)]
    [InlineData("scan.tiff", ViewerKind.Image)]
    [InlineData("pic.jfif", ViewerKind.Image)]
    [InlineData("icon.ico", ViewerKind.Image)]
    [InlineData("anim.webp", ViewerKind.Image)]
    [InlineData("logo.svg", ViewerKind.Svg)]
    [InlineData("song.opus", ViewerKind.Audio)]
    [InlineData("voice.oga", ViewerKind.Audio)]
    [InlineData("clip.m4v", ViewerKind.Video)]
    [InlineData("movie.webm", ViewerKind.Video)]
    [InlineData("app.exe", ViewerKind.Binary)]
    [InlineData("libz.so", ViewerKind.Binary)]
    [InlineData("data.sqlite", ViewerKind.Binary)]
    [InlineData("module.wasm", ViewerKind.Binary)]
    public void Kind_ComesFromTheExtension(string name, ViewerKind kind) => Assert.Equal(kind, ViewerKinds.Of(name));

    // Wavefront OBJ and .dat files can be text: they are shown as bytes only if they don't open as text.
    [Theory]
    [InlineData("model.obj")]
    [InlineData("table.dat")]
    [InlineData("Program.cs")]
    [InlineData("report.pdf")]
    [InlineData("noextension")]
    public void TextLikeAndForeignFiles_AreNotClaimed(string name) => Assert.Equal(ViewerKind.None, ViewerKinds.Of(name));

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }, ImageFormat.Png)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, ImageFormat.Jpeg)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, ImageFormat.Gif)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, ImageFormat.Gif)]
    [InlineData(new byte[] { 0x42, 0x4D, 0x36, 0x00 }, ImageFormat.Bmp)]
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x02, 0x00 }, ImageFormat.Ico)]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00 }, ImageFormat.Tiff)]
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, ImageFormat.Tiff)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x1A, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ImageFormat.WebP)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x1A, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, ImageFormat.Unknown)]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, ImageFormat.Unknown)]
    [InlineData(new byte[] { 0x89, 0x50 }, ImageFormat.Unknown)]
    public void ImageFormat_ComesFromTheSignature(byte[] header, ImageFormat format) => Assert.Equal(format, ImageFormats.Detect(header));

    [Fact]
    public void FormatName_IsTheRealFormat_OrTheExtension()
    {
        Assert.Equal("JPEG", ImageFormats.Name(ImageFormat.Jpeg, "photo.png"));
        Assert.Equal("HEIC", ImageFormats.Name(ImageFormat.Unknown, "photo.heic"));
    }

    [Theory]
    [InlineData(null, 0, false)]
    [InlineData(1, 0, false)]
    [InlineData(2, 0, true)]
    [InlineData(3, 180, false)]
    [InlineData(4, 180, true)]
    [InlineData(5, 270, true)]
    [InlineData(6, 90, false)]
    [InlineData(7, 90, true)]
    [InlineData(8, 270, false)]
    [InlineData(9, 0, false)]
    public void ExifOrientation_IsAFlipThenAClockwiseTurn(int? value, int rotation, bool flip) =>
        Assert.Equal(new ImageOrientation(rotation, flip), ExifOrientation.From(value));

    [Fact]
    public void QuarterTurns_SwapTheSides()
    {
        Assert.True(ExifOrientation.From(6).SwapsSides);
        Assert.False(ExifOrientation.From(3).SwapsSides);
        Assert.Equal(new PixelSize(3000, 4000), new PixelSize(4000, 3000).Transpose());
    }

    [Fact]
    public void ImageWithinLimits_IsDecodedInFull()
    {
        var photo = new PixelSize(4000, 3000);

        Assert.Equal(photo, ImageDecodeLimits.For(photo));
    }

    [Fact]
    public void HugeImage_IsReducedToTheMemoryLimit_KeepingProportions()
    {
        var reduced = ImageDecodeLimits.For(new PixelSize(8000, 6000));

        Assert.InRange((double)reduced.Pixels, ImageDecodeLimits.MaxPixels * 0.999, ImageDecodeLimits.MaxPixels * 1.001);
        Assert.Equal(8000.0 / 6000, (double)reduced.Width / reduced.Height, 2);
    }

    [Fact]
    public void LongPanorama_IsReducedToTheTextureSide()
    {
        var reduced = ImageDecodeLimits.For(new PixelSize(30000, 1000));

        Assert.Equal(ImageDecodeLimits.MaxSide, reduced.Width);
        Assert.Equal(273, reduced.Height);
    }
}
