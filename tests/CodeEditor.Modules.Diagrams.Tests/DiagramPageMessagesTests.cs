using System.Text.Json;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;
using CodeEditor.Modules.Diagrams.ViewModels;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Messages between the preview tab and its page: the full state (diagrams, captions, placeholder, theme, zoom) and the
/// page's replies (wheel and keyboard zoom); unknown messages are dropped.
/// </summary>
public sealed class DiagramPageMessagesTests : IDisposable
{
    private readonly DiagramsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Show_CarriesDiagramsThemeAndZoom()
    {
        _fixture.AddFile("README.md", "```mermaid\npie\n```\n\n```mermaid\ngraph TD\n  A --> !!\n```");
        using var preview = await _fixture.PreviewAsync("README.md");

        using var message = JsonDocument.Parse(DiagramPageMessages.Show(preview, new Dictionary<string, string> { ["background"] = "#1F1F1F" }));

        var root = message.RootElement;
        Assert.Equal("show", root.GetProperty("type").GetString());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(("Схема 1 · строка 2", false), (item.GetProperty("caption").GetString(), item.GetProperty("stale").GetBoolean()));
        Assert.StartsWith("<svg", item.GetProperty("svg").GetString(), StringComparison.Ordinal);
        Assert.Equal(("dark", "#1F1F1F"), (root.GetProperty("theme").GetProperty("scheme").GetString(), root.GetProperty("theme").GetProperty("background").GetString()));
        Assert.Equal(1, root.GetProperty("zoom").GetDouble());
    }

    [Theory]
    [InlineData("""{"type":"zoom","value":1.5}""", "zoom", 1.5, null)]
    [InlineData("""{"type":"key","name":"zoomIn"}""", "key", null, "zoomIn")]
    [InlineData("""{"type":"ready"}""", "ready", null, null)]
    public void PageMessages_AreRead(string json, string type, double? zoom, string? key) =>
        Assert.Equal(new DiagramPageMessage(type, zoom, key), DiagramPageMessages.Read(json));

    [Theory]
    [InlineData("\"text\"")]
    [InlineData("{\"value\":1}")]
    [InlineData("not json")]
    public void ForeignMessages_AreIgnored(string json) => Assert.Null(DiagramPageMessages.Read(json));
}
