using CodeEditor.Modules.Diagrams.Services.Export;

namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// The Mermaid diagram renderer (ADR 0035). Implemented in <c>CodeEditor.Modules.Diagrams.Wpf</c>: one hidden WebView2
/// page per module, created on first call. Methods may be called from any thread. Error lines are 1-based lines of the
/// given text, already adjusted for Mermaid's service lines (front matter, directives, comments).
/// </summary>
/// <remarks>Without a window (agent benchmark) there is no renderer and hence no <c>diagram</c> agent tool.</remarks>
public interface IDiagramRenderer
{
    /// <summary>Checks that the diagram parses and renders without errors.</summary>
    /// <returns>The Mermaid diagram type (<c>flowchart-v2</c>, <c>sequence</c>) or an error with its line.</returns>
    /// <exception cref="DiagramRendererException">The renderer is unavailable.</exception>
    Task<DiagramResult<string>> CheckAsync(string text, CancellationToken cancellationToken);

    /// <returns>SVG markup or an error with its line.</returns>
    /// <exception cref="DiagramRendererException">The renderer is unavailable.</exception>
    Task<DiagramResult<string>> RenderSvgAsync(string text, DiagramTheme theme, CancellationToken cancellationToken);

    /// <returns>A PNG image on the theme background or an error with its line.</returns>
    /// <exception cref="DiagramRendererException">The renderer is unavailable.</exception>
    Task<DiagramResult<byte[]>> RenderPngAsync(string text, DiagramTheme theme, DiagramPngSize size, CancellationToken cancellationToken);
}
