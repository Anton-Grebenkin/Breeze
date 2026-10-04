using System.Collections.Immutable;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// Preview images from render results: a successful render replaces the previous image; on a text error the last
/// successful image of the same diagram stays, marked stale, so the diagram does not flicker while being typed; a
/// diagram that never rendered successfully is not shown. <see cref="Align"/> decides which previous diagram is "the
/// same".
/// </summary>
/// <param name="caption">The diagram caption: number and line for Markdown blocks, none for a Mermaid file.</param>
internal sealed class PreviewPictures(Func<DiagramSource, string?> caption)
{
    // Diagrams of the previous display in order: the text and the image shown.
    private List<(string Text, string? Svg)> _previous = [];

    public (ImmutableArray<DiagramPreviewItem> Items, IReadOnlyList<DiagramProblem> Problems) Update(
        IReadOnlyList<DiagramSource> diagrams, IReadOnlyList<DiagramResult<string>> results)
    {
        var earlier = Align(diagrams);
        var next = new List<(string Text, string? Svg)>(diagrams.Count);
        var items = ImmutableArray.CreateBuilder<DiagramPreviewItem>(diagrams.Count);
        var problems = new List<DiagramProblem>();
        for (var i = 0; i < diagrams.Count; i++)
        {
            var result = results[i];
            var svg = result.IsSuccess ? result.Value : earlier[i] is { } position ? _previous[position].Svg : null;
            if (!result.IsSuccess)
            {
                problems.Add(DiagramErrors.Locate(diagrams[i], result.Error!));
            }

            if (svg is not null)
            {
                items.Add(new DiagramPreviewItem(diagrams[i].Index, svg, !result.IsSuccess) { Caption = caption(diagrams[i]) });
            }

            next.Add((diagrams[i].Text, svg));
        }

        _previous = next;
        return (items.ToImmutable(), problems);
    }

    /// <summary>
    /// The position of each diagram in the previous display. Diagrams with the same text match in order. Edited ones
    /// lie between matches: if the same neighbors had as many diagrams between them before, they match in order;
    /// otherwise a block was inserted or deleted and the diagram gets no previous image, so another diagram's image is
    /// never shown under its caption. O(n·m) in the number of diagrams per file (a few to dozens), once per render.
    /// </summary>
    private int?[] Align(IReadOnlyList<DiagramSource> diagrams)
    {
        var earlier = new int?[diagrams.Count];
        var from = 0;
        for (var i = 0; i < diagrams.Count; i++)
        {
            var match = _previous.FindIndex(from, entry => entry.Text == diagrams[i].Text);
            if (match >= 0)
            {
                earlier[i] = match;
                from = match + 1;
            }
        }

        var newStart = 0;
        var oldStart = 0;
        for (var i = 0; i <= diagrams.Count; i++)
        {
            if (i < diagrams.Count && earlier[i] is null)
            {
                continue;
            }

            var oldEnd = i < diagrams.Count ? earlier[i]!.Value : _previous.Count;
            if (i - newStart == oldEnd - oldStart)
            {
                for (var offset = 0; offset < i - newStart; offset++)
                {
                    earlier[newStart + offset] = oldStart + offset;
                }
            }

            newStart = i + 1;
            oldStart = oldEnd + 1;
        }

        return earlier;
    }
}
