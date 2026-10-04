using System.Collections.Immutable;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>
/// A text document (Word or a presentation) as a sequence of blocks. The model's Markdown and the user's view are built
/// from it; the file format is no longer visible here.
/// </summary>
public sealed record RichDocument(ImmutableArray<DocumentBlock> Blocks)
{
    /// <summary>Number of presentation slides.</summary>
    public int SlideCount => Blocks.Count(block => block.Kind == DocumentBlockKind.Slide);
}
