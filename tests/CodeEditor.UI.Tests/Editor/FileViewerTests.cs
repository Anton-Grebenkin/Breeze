using CodeEditor.UI.Tests.Infrastructure;

namespace CodeEditor.UI.Tests.Editor;

/// <summary>
/// File viewers on the real window (ADR 0037): an image with its size in the summary; a file that can't open as text in
/// the hex viewer; SVG as a WebView2 page, with the palette not covered by it.
/// </summary>
public sealed class FileViewerTests : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);

    private readonly string _folder;

    public FileViewerTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "assets")).FullName;
        SampleImages.WriteBmp(Path.Combine(_folder, "picture.bmp"), 64, 48);
        File.WriteAllBytes(Path.Combine(_folder, "blob.xyz"), [.. "MZ"u8, .. new byte[30], .. "CodeEditor"u8]);
        File.WriteAllText(Path.Combine(_folder, "logo.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\"><rect width=\"120\" height=\"80\" rx=\"12\" fill=\"#3794FF\"/></svg>");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void ImageHexAndSvg_OpenInViewers()
    {
        using var session = AppSession.WithArguments(_folder);

        session.WaitFor("Explorer.Node.picture.bmp").GuardedDoubleClick();
        session.WaitFor("ImageViewer");
        session.WaitForText("FileViewer.Summary", text => text.StartsWith("64 × 48", StringComparison.Ordinal), LoadTimeout);
        session.SaveScreenshot("viewers-image");

        // Unknown extension with zero bytes: it can't open as text, so it's shown as bytes.
        session.WaitFor("Explorer.Node.blob.xyz").GuardedDoubleClick();
        session.WaitFor("HexViewer.Row.00000000");
        session.WaitFor("HexViewer.Row.00000020");
        session.SaveScreenshot("viewers-hex");

        session.WaitFor("Explorer.Node.logo.svg").GuardedDoubleClick();
        session.WaitFor("SvgViewer");
        session.WaitForText("FileViewer.Summary", text => text.StartsWith("120 × 80", StringComparison.Ordinal), LoadTimeout);
        Assert.Null(session.TryFind("SvgViewer.Unavailable"));
        PaletteAirspace.AssertPaletteIsNotCovered(session);
        session.SaveScreenshot("viewers-svg");
    }
}
