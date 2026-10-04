using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Diagrams.Resources;

namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>
/// Diagrams from the <c>diagram</c> tool arguments: Mermaid text or a workspace file (<c>.mmd</c>, <c>.mermaid</c>,
/// Markdown), with unsaved edits if it is open. Errors are explanations for the model (<see cref="AgentToolException"/>).
/// </summary>
public sealed class DiagramToolInputs(IWorkspace workspace, DiagramTextReader reader)
{
    public const string TextName = "text";

    /// <param name="block">The Markdown block number from 1; <c>null</c> for all blocks.</param>
    public async Task<DiagramToolInput> ResolveAsync(string? path, string? text, int? block)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            // The model may wrap the diagram in ```mermaid as in a chat answer; then the block contents are the diagrams.
            var fenced = MarkdownMermaidBlocks.Find(text);
            return new DiagramToolInput(TextName, null, fenced.Count > 0 ? fenced : [new DiagramSource(text, FirstLine: 1, Index: 1)]);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AgentToolException(Strings.ToolPathOrText);
        }

        var full = WorkspacePaths.Resolve(workspace, path);
        var name = workspace.RelativePath(full);
        SensitivePaths.EnsureReadable(name);
        if (!DiagramFiles.CanContainDiagrams(full))
        {
            throw new AgentToolException(Format(Strings.ToolNotDiagramFile, name));
        }

        var diagrams = DiagramFiles.Extract(full, await ReadAsync(full, name));
        if (diagrams.Count == 0)
        {
            throw new AgentToolException(Format(Strings.ToolNoDiagrams, name));
        }

        return new DiagramToolInput(name, full, Select(diagrams, block, name));
    }

    private async Task<string> ReadAsync(string path, string name)
    {
        try
        {
            return await reader.ReadAsync(path) ?? throw new AgentToolException(Format(Strings.ToolFileNotFound, name));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentToolException(Format(Strings.ReadFailed, exception.Message));
        }
    }

    private static IReadOnlyList<DiagramSource> Select(IReadOnlyList<DiagramSource> diagrams, int? block, string name)
    {
        if (block is not { } index)
        {
            return diagrams;
        }

        return index >= 1 && index <= diagrams.Count
            ? [diagrams[index - 1]]
            : throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ToolNoSuchBlock, name, index, diagrams.Count));
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
