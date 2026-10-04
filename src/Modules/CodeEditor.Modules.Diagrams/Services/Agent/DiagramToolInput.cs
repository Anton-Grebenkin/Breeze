namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>What the agent asked to check, view or save.</summary>
/// <param name="Name">The diagram name shown to the model: a workspace-relative path or "text".</param>
/// <param name="FullPath">The file; <c>null</c> for a diagram from the <c>text</c> argument.</param>
/// <param name="Diagrams">Diagrams in order: the whole file, all Markdown blocks or one block.</param>
public sealed record DiagramToolInput(string Name, string? FullPath, IReadOnlyList<DiagramSource> Diagrams)
{
    /// <summary>The image name for the model: the path plus the Markdown block number, e.g. "docs/README.md#2".</summary>
    public string ImageName(DiagramSource diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        return FullPath is not null && DiagramFiles.IsMarkdown(FullPath) ? $"{Name}#{diagram.Index}" : Name;
    }
}
