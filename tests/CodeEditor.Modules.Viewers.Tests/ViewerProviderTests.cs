using CodeEditor.Core.Documents;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Modules.Viewers.ViewModels;
using static CodeEditor.Modules.Viewers.Tests.Infrastructure.ViewersFixture;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Viewer provider: which files it takes, with what priority, which of them are text, and the hex fallback for files
/// that failed to open as text. Creating a viewer reads nothing.
/// </summary>
public sealed class ViewerProviderTests : IDisposable
{
    private readonly ViewersFixture _fixture = new();
    private readonly ViewerProvider _provider;

    public ViewerProviderTests() => _provider = new ViewerProvider(_fixture.Context);

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("photo.jpeg", typeof(ImageViewerViewModel))]
    [InlineData("logo.SVG", typeof(SvgViewerViewModel))]
    [InlineData("song.flac", typeof(MediaViewerViewModel))]
    [InlineData("clip.mov", typeof(MediaViewerViewModel))]
    [InlineData("lib.dll", typeof(HexViewerViewModel))]
    public void KnownFiles_OpenInTheirViewer_WithoutReading(string name, Type expected)
    {
        Assert.True(_provider.CanOpen(name));
        using var viewer = (ViewerViewModel)_provider.CreateViewer(PathOf(name));

        Assert.IsType(expected, viewer);
        Assert.Equal(0, _fixture.Decoder.Calls);
        Assert.Empty(_fixture.Bytes.Reads);
        Assert.False(viewer.IsLoading);
    }

    [Fact]
    public void MediaViewer_KnowsAudioFromVideo()
    {
        using var audio = (MediaViewerViewModel)_provider.CreateViewer(PathOf("a.m4a"));
        using var video = (MediaViewerViewModel)_provider.CreateViewer(PathOf("v.mp4"));

        Assert.Equal((ViewerKind.Audio, false), (audio.Kind, audio.IsVideo));
        Assert.Equal((ViewerKind.Video, true), (video.Kind, video.IsVideo));
    }

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("report.pdf")]
    [InlineData("model.obj")]
    public void OtherFiles_AreLeftToOthers(string name) => Assert.False(_provider.CanOpen(name));

    [Fact]
    public void OnlySvg_IsText()
    {
        Assert.True(_provider.IsText("logo.svg"));
        Assert.False(_provider.IsText("photo.png"));
        Assert.False(_provider.IsText("lib.dll"));
    }

    // Documents (PDF, Office) have priority 10: when extensions overlap, the specialized viewer wins.
    [Fact]
    public void Priority_IsBelowTheDocumentViewers() => Assert.InRange(_provider.Priority, 1, 9);

    [Theory]
    [InlineData(DocumentOpenFailure.Binary, true)]
    [InlineData(DocumentOpenFailure.TooLarge, true)]
    [InlineData(DocumentOpenFailure.Unreadable, false)]
    public void FilesThatDidNotOpenAsText_AreShownAsBytes(DocumentOpenFailure failure, bool shown) =>
        Assert.Equal(shown, _provider.OpensInsteadOfText(PathOf("unknown.xyz"), failure));

    [Fact]
    public void FallbackViewer_ForAnyFile_IsTheHexViewer()
    {
        using var viewer = _provider.CreateViewer(PathOf("big.log")) as IDisposable;

        Assert.IsType<HexViewerViewModel>(viewer);
    }
}
