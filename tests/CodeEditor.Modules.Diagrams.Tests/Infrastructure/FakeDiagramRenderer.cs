using System.Text;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.Tests.Infrastructure;

/// <summary>
/// Renderer without a browser: SVG is the diagram text in a tag, PNG is the bytes of "png:" plus the text. A line with
/// "!!" is a parse error like Mermaid's "Parse error on line N:", counted from the diagram start. The diagram type is
/// the first word of the text.
/// </summary>
internal sealed class FakeDiagramRenderer : IDiagramRenderer
{
    public const string ErrorMark = "!!";

    public List<(string Text, DiagramTheme Theme)> Rendered { get; } = [];

    public List<(string Text, DiagramPngSize Size)> Rasterized { get; } = [];

    public List<string> Checked { get; } = [];

    /// <summary>Renderer unavailable: every call throws this exception.</summary>
    public DiagramRendererException? Failure { get; set; }

    public Task<DiagramResult<string>> CheckAsync(string text, CancellationToken cancellationToken)
    {
        Checked.Add(text);
        return Task.FromResult(Error(text) is { } error ? DiagramResult<string>.Failure(error) : DiagramResult<string>.Success(Type(text)));
    }

    public Task<DiagramResult<string>> RenderSvgAsync(string text, DiagramTheme theme, CancellationToken cancellationToken)
    {
        Rendered.Add((text, theme));
        return Task.FromResult(Error(text) is { } error ? DiagramResult<string>.Failure(error) : DiagramResult<string>.Success(Svg(text, theme)));
    }

    public Task<DiagramResult<byte[]>> RenderPngAsync(string text, DiagramTheme theme, DiagramPngSize size, CancellationToken cancellationToken)
    {
        Rasterized.Add((text, size));
        return Task.FromResult(Error(text) is { } error ? DiagramResult<byte[]>.Failure(error) : DiagramResult<byte[]>.Success(Png(text)));
    }

    public static string Svg(string text, DiagramTheme theme) => $"<svg theme=\"{theme}\">{text.Trim()}</svg>";

    public static byte[] Png(string text) => Encoding.UTF8.GetBytes("png:" + text.Trim());

    private DiagramError? Error(string text)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        var lines = text.Split('\n');
        var line = Array.FindIndex(lines, candidate => candidate.Contains(ErrorMark, StringComparison.Ordinal));
        return line < 0 ? null : new DiagramError($"Parse error on line {line + 1}:\n{lines[line].Trim()}\n---^\nExpecting 'NODE', got '!!'", line + 1);
    }

    private static string Type(string text) => text.Trim().Split((char[])[' ', '\n'], 2)[0];
}
