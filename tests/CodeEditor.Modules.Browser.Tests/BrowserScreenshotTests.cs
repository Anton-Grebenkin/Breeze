using System.Text.Json.Nodes;
using CodeEditor.Modules.Browser.Services;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// DevTools screenshot parameters: from the scroll position, panel width, page height within the limit; the response is
/// a base64 image, and without one it is a browser error.
/// </summary>
public sealed class BrowserScreenshotTests
{
    private const string Metrics = """
        {"cssLayoutViewport":{"pageX":0,"pageY":300,"clientWidth":1200,"clientHeight":280},"cssContentSize":{"x":0,"y":0,"width":1200,"height":9000}}
        """;

    [Fact]
    public void Request_ClipsFromTheScrollPosition_UpToTheHeightLimit()
    {
        var request = JsonNode.Parse(BrowserScreenshot.Request(Metrics, BrowserImageFormat.Png))!;

        Assert.Equal("png", request["format"]!.GetValue<string>());
        Assert.True(request["captureBeyondViewport"]!.GetValue<bool>());
        var clip = request["clip"]!;
        Assert.Equal((0d, 300d, 1200d, (double)BrowserScreenshot.MaxHeight), (clip["x"]!.GetValue<double>(), clip["y"]!.GetValue<double>(), clip["width"]!.GetValue<double>(), clip["height"]!.GetValue<double>()));
    }

    // A page shorter than the panel captures the whole visible area.
    [Fact]
    public void Request_ShortPage_TakesTheVisibleArea()
    {
        const string metrics = """{"cssLayoutViewport":{"pageX":0,"pageY":0,"clientWidth":800,"clientHeight":280},"cssContentSize":{"height":100}}""";

        var clip = JsonNode.Parse(BrowserScreenshot.Request(metrics, BrowserImageFormat.Jpeg))!["clip"]!;

        Assert.Equal(280d, clip["height"]!.GetValue<double>());
    }

    [Fact]
    public void Request_WithoutMetrics_TakesTheViewport_JpegHasQuality()
    {
        var request = JsonNode.Parse(BrowserScreenshot.Request("{}", BrowserImageFormat.Jpeg))!;

        Assert.Null(request["clip"]);
        Assert.Equal(("jpeg", 80), (request["format"]!.GetValue<string>(), request["quality"]!.GetValue<int>()));
    }

    [Fact]
    public void Data_DecodesTheImage_OrFails()
    {
        Assert.Equal([1, 2, 3], BrowserScreenshot.Data("""{"data":"AQID"}"""));
        Assert.Throws<BrowserException>(() => BrowserScreenshot.Data("""{"error":"no"}"""));
        Assert.Throws<BrowserException>(() => BrowserScreenshot.Data("""{"data":"не base64"}"""));
    }
}
