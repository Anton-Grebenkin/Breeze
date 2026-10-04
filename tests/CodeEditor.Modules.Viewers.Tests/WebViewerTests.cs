using CodeEditor.Modules.Viewers.Commands;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Modules.Viewers.ViewModels;
using static CodeEditor.Modules.Viewers.Tests.Infrastructure.ViewersFixture;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// SVG and media on a WebView2 page: the file address goes through its folder's host with a version; page replies are
/// picture size, zoom, media info and errors; "Open as Text" for SVG.
/// </summary>
public sealed class WebViewerTests : IDisposable
{
    private readonly ViewersFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Source_IsTheFileUnderItsFolderHost_WithANewVersionPerLoad()
    {
        using var viewer = Svg("логотип компании.svg");

        await viewer.EnsureLoadedAsync();
        var first = viewer.Source!;
        await viewer.RefreshAsync();

        Assert.Equal(ViewerAddresses.HostFor(Root), first.Host);
        Assert.Equal("/логотип компании.svg", Uri.UnescapeDataString(first.AbsolutePath));
        Assert.DoesNotContain(' ', first.AbsoluteUri);
        Assert.NotEqual(first, viewer.Source);
        Assert.Equal(2, viewer.Revision);
        Assert.Equal(Root, viewer.Folder);
    }

    [Fact]
    public async Task MissingFile_IsAnError()
    {
        using var viewer = new SvgViewerViewModel(PathOf("нет.svg"), _fixture.Context);

        await viewer.EnsureLoadedAsync();

        Assert.Contains("Файл удалён или перемещён", viewer.Error, StringComparison.Ordinal);
        Assert.Null(viewer.Source);
    }

    [Fact]
    public async Task SvgSize_FromThePage_GoesToTheSummary()
    {
        using var viewer = Svg("logo.svg");
        await viewer.EnsureLoadedAsync();
        var before = viewer.Summary;

        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Size) { Width = 120, Height = 80.4 });

        Assert.Equal("SVG · 6 байт", before);
        Assert.Equal("120 × 81 · SVG · 6 байт", viewer.Summary);
        Assert.Equal(new PixelSize(120, 81), viewer.NaturalSize);
    }

    [Fact]
    public async Task BrokenSvg_IsReported_AndADrawnOneClearsTheError()
    {
        using var viewer = Svg("logo.svg");
        await viewer.EnsureLoadedAsync();

        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Error));
        var broken = viewer.Error;
        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Size) { Width = 10, Height = 10 });

        Assert.Contains("не правильный SVG", broken, StringComparison.Ordinal);
        Assert.Null(viewer.Error);
    }

    [Fact]
    public void PageZoom_IsMirrored()
    {
        using var viewer = Svg("logo.svg");

        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Zoom) { Value = 0.42, Fit = true });

        Assert.Equal((0.42, true), (viewer.Zoom.Scale, viewer.Zoom.IsFit));
    }

    [Fact]
    public async Task Svg_OpensAsText_ThroughTheCommand()
    {
        using var viewer = Svg("logo.svg");

        Assert.True(viewer.CanOpenAsText);
        await viewer.OpenAsTextCommand.ExecuteAsync(null);

        Assert.Contains((ViewerCommands.OpenAsTextId, (object?)PathOf("logo.svg")), _fixture.Commands.Executed);
    }

    [Fact]
    public async Task Video_Summary_HasKindFrameDurationAndSize()
    {
        _fixture.Files.AddBytes(PathOf("clip.mp4"), new byte[3 * 1024 * 1024]);
        using var viewer = new MediaViewerViewModel(PathOf("clip.mp4"), ViewerKind.Video, _fixture.Context);
        await viewer.EnsureLoadedAsync();

        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Metadata) { Duration = 3727.4, Width = 1920, Height = 1080 });

        Assert.True(viewer.IsVideo);
        Assert.Equal("Видео MP4 · 1920 × 1080 · 1:02:07 · 3 МБ", viewer.Summary);
    }

    [Fact]
    public async Task Audio_WithoutKnownDuration_SkipsIt()
    {
        _fixture.Files.AddBytes(PathOf("song.mp3"), new byte[512]);
        using var viewer = new MediaViewerViewModel(PathOf("song.mp3"), ViewerKind.Audio, _fixture.Context);
        await viewer.EnsureLoadedAsync();

        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Metadata) { Duration = null });
        var unknown = viewer.Summary;
        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Metadata) { Duration = 187 });

        Assert.Equal("Аудио MP3 · 512 байт", unknown);
        Assert.Equal("Аудио MP3 · 3:07 · 512 байт", viewer.Summary);
    }

    [Fact]
    public async Task UnsupportedCodec_IsExplained()
    {
        _fixture.Files.AddBytes(PathOf("old.ogv"), new byte[16]);
        using var viewer = new MediaViewerViewModel(PathOf("old.ogv"), ViewerKind.Video, _fixture.Context);
        await viewer.EnsureLoadedAsync();

        viewer.Receive(new ViewerPageMessage(ViewerPageMessages.Error));

        Assert.True(viewer.CannotPlay);
        Assert.Contains("Откройте его во внешнем приложении", viewer.Error, StringComparison.Ordinal);
        Assert.False(viewer.CanOpenAsText);
    }

    private SvgViewerViewModel Svg(string name)
    {
        _fixture.Files.AddFile(PathOf(name), "<svg/>");
        return new SvgViewerViewModel(PathOf(name), _fixture.Context);
    }
}
