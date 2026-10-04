using System.Text.Json;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Renderer page calls via <c>Runtime.evaluate</c>: the diagram text goes only as a JSON string (quotes and line breaks
/// can't break or extend the script); replies are a value, a diagram error with the user's text line, or a renderer
/// failure.
/// </summary>
public sealed class MermaidProtocolTests
{
    [Fact]
    public void Request_PutsTheTextAsJson_AndWaitsForThePromise()
    {
        var source = MermaidSource.Prepare("graph TD\n  A[\"x\"); alert(1); (\"\"] --> B");

        using var request = JsonDocument.Parse(MermaidProtocol.Svg(source, DiagramTheme.Dark));

        var root = request.RootElement;
        Assert.True(root.GetProperty("awaitPromise").GetBoolean());
        Assert.True(root.GetProperty("returnByValue").GetBoolean());
        Assert.Equal($"diagrams.svg({JsonSerializer.Serialize(source.Text)}, \"dark\")", root.GetProperty("expression").GetString());
    }

    [Fact]
    public void PngRequest_WritesNumbersInvariantly()
    {
        var expression = Expression(MermaidProtocol.Png(MermaidSource.Prepare("pie"), DiagramTheme.Light, new DiagramPngSize(1.5, 2000)));

        Assert.EndsWith(", \"default\", 1.5, 2000)", expression, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_ReturnsTheValue()
    {
        var result = MermaidProtocol.ReadText(Answer("""{"ok":true,"svg":"<svg/>"}"""), "svg", MermaidSource.Prepare("graph TD"));

        Assert.True(result.IsSuccess);
        Assert.Equal("<svg/>", result.Value);
    }

    // Mermaid counts lines after the front matter: an error on "line 2" is line 5 of the text.
    [Fact]
    public void DiagramError_PointsIntoTheText()
    {
        var source = MermaidSource.Prepare("---\ntitle: x\n---\ngraph TD\n  A -->");

        var result = MermaidProtocol.ReadText(Answer("""{"ok":false,"message":"Parse error on line 2:","line":2}"""), "svg", source);

        Assert.False(result.IsSuccess);
        Assert.Equal(new DiagramError("Parse error on line 2:", 5), result.Error);
    }

    [Fact]
    public void ErrorWithoutLine_PointsToTheFirstDiagramLine()
    {
        var result = MermaidProtocol.ReadText(Answer("""{"ok":false,"message":"No diagram type detected","line":null}"""), "type", MermaidSource.Prepare("\n\nhello"));

        Assert.Equal(3, result.Error?.Line);
    }

    [Fact]
    public void Png_IsDecodedFromBase64()
    {
        byte[] png = [1, 2, 3];

        var result = MermaidProtocol.ReadPng(Answer($$"""{"ok":true,"png":"{{Convert.ToBase64String(png)}}"}"""), MermaidSource.Prepare("pie"));

        Assert.Equal(png, result.Value);
    }

    [Fact]
    public void Ready_WaitsForMermaid()
    {
        Assert.Equal("diagrams.ready()", Expression(MermaidProtocol.Ready));
        MermaidProtocol.ReadReady(Answer("""{"ok":true}"""));
        Assert.Throws<DiagramRendererException>(() => MermaidProtocol.ReadReady(Answer("""{"ok":false,"unavailable":true,"message":"offline"}""")));
    }

    [Fact]
    public void MermaidNotLoaded_IsARendererFailure()
    {
        var error = Assert.Throws<DiagramRendererException>(() =>
            MermaidProtocol.ReadText(Answer("""{"ok":false,"unavailable":true,"message":"cannot load mermaid.min.js"}"""), "svg", MermaidSource.Prepare("pie")));

        Assert.Contains("cannot load mermaid.min.js", error.Message, StringComparison.Ordinal);
        Assert.Contains("mermaid.min.js", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"result":{"type":"object","subtype":"error"},"exceptionDetails":{"text":"Uncaught","exception":{"description":"TypeError: x is not a function"}}}""", "TypeError: x is not a function")]
    [InlineData("""{"result":{"type":"undefined"}}""", "не ответил")]
    [InlineData("not json", "не ответил")]
    public void BrokenAnswers_AreRendererFailures(string response, string expected)
    {
        var error = Assert.Throws<DiagramRendererException>(() => MermaidProtocol.ReadText(response, "svg", MermaidSource.Prepare("pie")));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    private static string Answer(string value) => $$$"""{"result":{"type":"object","value":{{{value}}}}}""";

    private static string Expression(string request)
    {
        using var document = JsonDocument.Parse(request);
        return document.RootElement.GetProperty("expression").GetString()!;
    }
}
