using System.Globalization;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.ViewModels;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Image zoom: fit to window without stretching small images, button steps, wheel, 100 %, limits, unsmoothed pixels at
/// high zoom; scrolling keeps the point under the pointer.
/// </summary>
public sealed class ZoomTests
{
    [Fact]
    public void ByDefault_BigImage_FitsTheWindow()
    {
        var zoom = new ZoomState();

        zoom.SetContent(new PixelSize(4000, 2000));
        zoom.SetViewport(1000, 800);

        Assert.True(zoom.IsFit);
        Assert.Equal(0.25, zoom.Scale);
    }

    [Fact]
    public void Fit_DoesNotStretchASmallImage()
    {
        var zoom = new ZoomState();

        zoom.SetViewport(1000, 800);
        zoom.SetContent(new PixelSize(32, 32));

        Assert.Equal(1, zoom.Scale);
    }

    [Fact]
    public void Fit_FollowsTheWindow_UntilAScaleIsChosen()
    {
        var zoom = new ZoomState();
        zoom.SetContent(new PixelSize(2000, 1000));
        zoom.SetViewport(1000, 1000);

        zoom.SetViewport(500, 1000);
        var shrunk = zoom.Scale;
        zoom.ActualSize();
        zoom.SetViewport(250, 1000);

        Assert.Equal(0.25, shrunk);
        Assert.Equal(1, zoom.Scale);
        Assert.False(zoom.IsFit);
    }

    [Fact]
    public void Steps_GoLikeVsCode_FromTheCurrentScale()
    {
        var zoom = new ZoomState();

        zoom.ZoomIn();
        var first = zoom.Scale;
        zoom.ZoomIn();
        var second = zoom.Scale;
        zoom.ZoomOut();
        zoom.ZoomOut();
        zoom.ZoomOut();

        Assert.Equal((1.5, 2.0, 0.9), (first, second, zoom.Scale));
    }

    [Fact]
    public void StepFromAWheelScale_GoesToTheNextStep()
    {
        var zoom = new ZoomState();
        zoom.Report(1.23, isFit: false);

        zoom.ZoomIn();

        Assert.Equal(1.5, zoom.Scale);
    }

    [Fact]
    public void Wheel_ZoomsSmoothly_AndLeavesFit()
    {
        var zoom = new ZoomState();

        zoom.Wheel(120);
        var up = zoom.Scale;
        zoom.Wheel(-240);

        Assert.Equal(1.2, up, 6);
        Assert.Equal(1 / 1.2, zoom.Scale, 6);
        Assert.False(zoom.IsFit);
    }

    [Fact]
    public void Scale_StaysWithinLimits()
    {
        var zoom = new ZoomState();

        for (var step = 0; step < 50; step++)
        {
            zoom.ZoomIn();
        }

        var max = zoom.Scale;
        zoom.Report(double.NaN, isFit: false);

        Assert.Equal(ZoomState.Max, max);
        Assert.Equal(1, zoom.Scale);
        Assert.Equal(ZoomState.Min, ZoomState.Clamp(0));
    }

    [Fact]
    public void LargeScale_ShowsPixels()
    {
        var zoom = new ZoomState();

        zoom.Report(2, isFit: false);
        var smooth = zoom.IsPixelated;
        zoom.ZoomIn();

        Assert.False(smooth);
        Assert.True(zoom.IsPixelated);
    }

    [Fact]
    public void Text_IsAPercentage()
    {
        var zoom = new ZoomState();

        zoom.Report(0.5, isFit: false);

        Assert.Equal(0.5.ToString("P0", CultureInfo.CurrentCulture), zoom.Text);
    }

    [Fact]
    public void Fit_Command_ReturnsToTheWindowSize()
    {
        var zoom = new ZoomState();
        zoom.SetContent(new PixelSize(2000, 2000));
        zoom.SetViewport(1000, 1000);
        zoom.ZoomIn();

        zoom.FitCommand.Execute(null);

        Assert.True(zoom.IsFit);
        Assert.Equal(0.5, zoom.Scale);
    }

    // Viewport 100, pointer at 30, image 200 → 400 with no scroll: image point 30/200 must stay under the pointer.
    [Fact]
    public void Anchor_KeepsThePointUnderThePointer() => Assert.Equal(30, ZoomAnchor.Offset(100, 30, 0, 200, 400));

    [Fact]
    public void Anchor_CentersTheZoom_FromACenteredImage()
    {
        // A 50 px image in a 100 px viewport spans 25..75: the viewport center is the image middle and stays centered.
        var offset = ZoomAnchor.Offset(100, 50, 0, 50, 200);

        Assert.Equal(50, offset);
    }

    [Fact]
    public void Anchor_DoesNotScroll_WhenTheImageFits() => Assert.Equal(0, ZoomAnchor.Offset(100, 80, 40, 400, 90));

    // A stale scroll offset (past the image edge): the new one stays within the image.
    [Fact]
    public void Anchor_StaysWithinTheImage() => Assert.Equal(300, ZoomAnchor.Offset(100, 100, 250, 300, 400));
}
