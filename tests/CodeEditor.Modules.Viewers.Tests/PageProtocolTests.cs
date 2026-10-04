using System.Text.Json;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Messages between a tab and its WebView2 pages, and their addresses: JSON both ways, a host per file folder,
/// navigation only to the module's own pages.
/// </summary>
public sealed class PageProtocolTests
{
    private static readonly Dictionary<string, string> Theme = new(StringComparer.Ordinal) { ["scheme"] = "dark", ["background"] = "#1F1F1F" };

    [Fact]
    public void ShowPicture_CarriesTheAddressThemeAndZoom()
    {
        using var fitted = JsonDocument.Parse(ViewerPageMessages.ShowPicture(new Uri("https://file-0.codeeditor.example/a%20b.svg?v=1"), null, Theme));
        using var chosen = JsonDocument.Parse(ViewerPageMessages.ShowPicture(new Uri("https://file-0.codeeditor.example/a.svg"), 2.5, Theme));

        var root = fitted.RootElement;
        Assert.Equal("show", root.GetProperty("type").GetString());
        Assert.Equal("https://file-0.codeeditor.example/a%20b.svg?v=1", root.GetProperty("src").GetString());
        Assert.Equal("#1F1F1F", root.GetProperty("theme").GetProperty("background").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("zoom").ValueKind);
        Assert.Equal(2.5, chosen.RootElement.GetProperty("zoom").GetDouble());
    }

    [Fact]
    public void ShowMedia_SaysWhetherItIsAVideo()
    {
        using var message = JsonDocument.Parse(ViewerPageMessages.ShowMedia(new Uri("https://file-0.codeeditor.example/clip.mp4"), video: true, Theme));

        Assert.True(message.RootElement.GetProperty("video").GetBoolean());
    }

    [Fact]
    public void SmallMessages_AreJson()
    {
        Assert.Equal("""{"type":"fit"}""", ViewerPageMessages.Fit);
        Assert.Equal("""{"type":"pause"}""", ViewerPageMessages.Pause);
        Assert.Equal("""{"type":"zoom","value":1.5}""", ViewerPageMessages.ZoomTo(1.5));
    }

    [Fact]
    public void PageAnswers_AreRead()
    {
        var zoom = ViewerPageMessages.Read("""{"type":"zoom","value":0.5,"fit":true}""");
        var metadata = ViewerPageMessages.Read("""{"type":"metadata","duration":null,"width":640,"height":360}""");

        Assert.Equal((ViewerPageMessages.Zoom, (double?)0.5, true), (zoom!.Type, zoom.Value, zoom.Fit));
        Assert.Equal(((double?)null, (double?)640, (double?)360), (metadata!.Duration, metadata.Width, metadata.Height));
    }

    [Theory]
    [InlineData("не json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"value":1}""")]
    [InlineData("""{"type":5}""")]
    public void ForeignMessages_AreIgnored(string json) => Assert.Null(ViewerPageMessages.Read(json));

    [Fact]
    public void Host_IsStablePerFolder_AndDiffersBetweenFolders()
    {
        var host = ViewerAddresses.HostFor(@"C:\work\images");

        Assert.Equal(host, ViewerAddresses.HostFor(@"c:\WORK\Images\"));
        Assert.NotEqual(host, ViewerAddresses.HostFor(@"C:\work\video"));
        Assert.Matches("^file-[0-9a-f]{16}\\.codeeditor\\.example$", host);
    }

    [Fact]
    public void FileAddress_EscapesTheName_AndCarriesTheVersion()
    {
        var address = ViewerAddresses.FileAddress(@"C:\Новая папка\#1 фото.png", "42-1");

        Assert.Equal(ViewerAddresses.HostFor(@"C:\Новая папка"), address.Host);
        Assert.Equal("/#1 фото.png", Uri.UnescapeDataString(address.AbsolutePath));
        Assert.Equal("?v=42-1", address.Query);
    }

    [Theory]
    [InlineData("https://viewers.codeeditor.example/svg.html", true)]
    [InlineData("https://file-0123456789abcdef.codeeditor.example/a.svg", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData(null, false)]
    public void Pages_StayOnTheirOwnAddresses(string? address, bool allowed) => Assert.Equal(allowed, ViewerAddresses.IsPage(address));
}
