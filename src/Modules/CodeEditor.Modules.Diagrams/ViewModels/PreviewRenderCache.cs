using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// Renders of the last display keyed by diagram text and theme, so an unchanged diagram is not rendered again. Editing
/// one block of a large Markdown file costs one render, not one per block. Only the last display is kept, so memory
/// does not grow. Errors are cached too: the same text gives the same error.
/// </summary>
internal sealed class PreviewRenderCache(IDiagramRenderer renderer)
{
    private Dictionary<(string Text, DiagramTheme Theme), DiagramResult<string>> _last = [];

    /// <exception cref="DiagramRendererException">The renderer is unavailable.</exception>
    public async Task<IReadOnlyList<DiagramResult<string>>> RenderAsync(
        IReadOnlyList<DiagramSource> diagrams, DiagramTheme theme, CancellationToken cancellationToken)
    {
        var next = new Dictionary<(string Text, DiagramTheme Theme), DiagramResult<string>>(diagrams.Count);
        var results = new List<DiagramResult<string>>(diagrams.Count);
        foreach (var diagram in diagrams)
        {
            var key = (diagram.Text, theme);
            if (!next.TryGetValue(key, out var result) && !_last.TryGetValue(key, out result))
            {
                result = await renderer.RenderSvgAsync(diagram.Text, theme, cancellationToken);
            }

            next[key] = result;
            results.Add(result);
        }

        _last = next;
        return results;
    }
}
