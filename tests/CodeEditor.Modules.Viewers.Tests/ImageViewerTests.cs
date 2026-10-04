using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Services;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Modules.Viewers.ViewModels;
using static CodeEditor.Modules.Viewers.Tests.Infrastructure.ViewersFixture;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Image viewer: decodes only when shown, info and hints, errors suggest opening in an external app, a large image is
/// released in a hidden tab, a changed file is reloaded after a pause.
/// </summary>
public sealed class ImageViewerTests : IDisposable
{
    private readonly ViewersFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Image_IsDecodedOnlyWhenShown()
    {
        using var viewer = Open();
        var before = _fixture.Decoder.Calls;

        viewer.SetShown(true);
        await viewer.EnsureLoadedAsync();

        Assert.Equal((0, 1), (before, _fixture.Decoder.Calls));
        Assert.NotNull(viewer.Picture);
    }

    [Fact]
    public async Task Summary_SaysSizeFormatAndFileSize()
    {
        _fixture.Decoder.Result = _ => FakeImageDecoder.Image(new PixelSize(1920, 1080), format: ImageFormat.Jpeg, length: 2048);
        using var viewer = Open("photo.png");

        await viewer.EnsureLoadedAsync();

        Assert.Equal("1920 × 1080 · JPEG · 2 КБ", viewer.Summary);
        Assert.Null(viewer.Note);
    }

    [Fact]
    public async Task ReducedAnimatedImage_IsExplained()
    {
        _fixture.Decoder.Result = _ => FakeImageDecoder.Image(new PixelSize(9000, 100), new PixelSize(8192, 91), ImageFormat.Gif, frames: 12);
        using var viewer = Open("anim.gif");

        await viewer.EnsureLoadedAsync();

        Assert.Contains("8192 × 91", viewer.Note, StringComparison.Ordinal);
        Assert.Contains("первый кадр из 12", viewer.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zoom_FitsTheDecodedImage()
    {
        _fixture.Decoder.Result = _ => FakeImageDecoder.Image(new PixelSize(4000, 1000));
        using var viewer = Open();
        viewer.Zoom.SetViewport(1000, 1000);

        await viewer.EnsureLoadedAsync();

        Assert.Equal(0.25, viewer.Zoom.Scale);
    }

    [Fact]
    public async Task WebPWithoutCodec_AsksForTheExtension_AndOffersTheExternalApp()
    {
        _fixture.Decoder.Result = _ => throw new ImageDecodeException(ImageDecodeFailure.NoCodec, ImageFormat.WebP, "No imaging component.");
        using var viewer = Open("pic.webp");

        await viewer.EnsureLoadedAsync();

        Assert.Contains("Расширения для изображений WebP", viewer.Error, StringComparison.Ordinal);
        Assert.True(viewer.SuggestsExternalApp);
        viewer.OpenExternalCommand.Execute(null);
        Assert.Equal([PathOf("pic.webp")], _fixture.Shell.Launched);
    }

    [Fact]
    public async Task DamagedImage_SaysSo_AndKeepsThePreviousPicture()
    {
        using var viewer = Open();
        await viewer.EnsureLoadedAsync();
        var picture = viewer.Picture;

        _fixture.Decoder.Result = _ => throw new ImageDecodeException(ImageDecodeFailure.Damaged, ImageFormat.Png, "Bad CRC.");
        await viewer.RefreshAsync();

        Assert.Contains("повреждена", viewer.Error, StringComparison.Ordinal);
        Assert.Same(picture, viewer.Picture);
    }

    [Fact]
    public async Task MissingFile_IsExplainedInRussian()
    {
        _fixture.Decoder.Result = path => throw new FileNotFoundException("Could not find file.", path);
        using var viewer = Open();

        await viewer.EnsureLoadedAsync();

        Assert.Contains("Файл удалён или перемещён", viewer.Error, StringComparison.Ordinal);
        Assert.False(viewer.SuggestsExternalApp);
    }

    [Fact]
    public async Task LargePicture_IsReleasedWhileHidden_AndDecodedAgainWhenShown()
    {
        _fixture.Decoder.Result = _ => FakeImageDecoder.Image(new PixelSize(4000, 3000));
        using var viewer = Open();
        viewer.SetShown(true);
        await viewer.EnsureLoadedAsync();

        viewer.SetShown(false);
        var hidden = viewer.Picture;
        viewer.SetShown(true);
        await WaitForAsync(() => viewer.Picture is not null);

        Assert.Null(hidden);
        Assert.Equal(2, _fixture.Decoder.Calls);
    }

    [Fact]
    public async Task ChangedWhileHidden_LargePicture_WaitsForTheTab()
    {
        _fixture.Decoder.Result = _ => FakeImageDecoder.Image(new PixelSize(4000, 3000));
        using var viewer = Open();
        viewer.SetShown(true);
        await viewer.EnsureLoadedAsync();
        viewer.SetShown(false);

        await viewer.RefreshAsync();

        Assert.Null(viewer.Picture);
        Assert.Equal(new PixelSize(4000, 3000), viewer.Image!.Size);
    }

    [Fact]
    public async Task SmallPicture_StaysWhileHidden()
    {
        using var viewer = Open();
        await viewer.EnsureLoadedAsync();

        viewer.SetShown(false);
        viewer.SetShown(true);

        Assert.NotNull(viewer.Picture);
        Assert.Equal(1, _fixture.Decoder.Calls);
    }

    [Fact]
    public async Task ChangedFile_IsDecodedAgainAfterAPause()
    {
        using var viewer = Open();
        await viewer.EnsureLoadedAsync();

        _fixture.ReportChange(PathOf("pic.png"));
        var immediately = _fixture.Decoder.Calls;
        _fixture.Time.Advance(ViewerViewModel.ReloadDelay);
        await WaitForAsync(() => _fixture.Decoder.Calls == 2);

        Assert.Equal(1, immediately);
    }

    [Fact]
    public async Task OtherFileChanges_AreIgnored()
    {
        using var viewer = Open();
        await viewer.EnsureLoadedAsync();

        _fixture.ReportChange(PathOf("other.png"));
        _fixture.Time.Advance(ViewerViewModel.ReloadDelay);

        Assert.Equal(1, _fixture.Decoder.Calls);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private ImageViewerViewModel Open(string name = "pic.png")
    {
        _fixture.Files.AddBytes(PathOf(name), [1, 2, 3]);
        return new ImageViewerViewModel(PathOf(name), _fixture.Context);
    }
}
