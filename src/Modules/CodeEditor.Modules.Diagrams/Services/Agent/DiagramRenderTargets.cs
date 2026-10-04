using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Diagrams.Services.Export;

namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>
/// Foreign files the <c>render</c> action would overwrite: an image with the export name exists but was not written by
/// the diagram export in this session (<see cref="DiagramWrites"/>), e.g. a human-drawn <c>arch.png</c>. For Markdown
/// without a block number every <c>name-N.svg</c> in the folder is checked: the block count in unsaved text may differ
/// from the file on disk. Decided from the call arguments, before rendering.
/// </summary>
public sealed class DiagramRenderTargets(IWorkspace workspace, IFileSystem fileSystem, DiagramWrites writes)
{
    /// <returns>
    /// Full paths of foreign files; empty when there is nothing to overwrite or the arguments are invalid (the tool
    /// explains the error).
    /// </returns>
    public IReadOnlyList<string> Foreign(IDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (Source(arguments) is not { } source || DiagramToolFormats.TryParse(Text(arguments, "format")) is not { } format)
        {
            return [];
        }

        return [.. Targets(source, format, Block(arguments)).Where(path => fileSystem.FileExists(path) && !writes.Contains(path))];
    }

    private IEnumerable<string> Targets(string source, DiagramFormat format, int? block)
    {
        if (!DiagramFiles.IsMarkdown(source) || block is not null)
        {
            return [DiagramExportPaths.For(source, format, block ?? 1)];
        }

        var folder = Path.GetDirectoryName(source) ?? string.Empty;
        var prefix = Path.GetFileNameWithoutExtension(source) + "-";
        var extension = DiagramExportPaths.Extension(format);
        return fileSystem.DirectoryExists(folder)
            ? fileSystem.EnumerateEntries(folder)
                .Where(entry => !entry.IsDirectory && IsNumbered(entry.Name, prefix, extension))
                .Select(entry => entry.FullPath)
            : [];
    }

    // "README-12.svg" for the prefix "README-" and the extension ".svg".
    private static bool IsNumbered(string name, string prefix, string extension)
    {
        if (name.Length <= prefix.Length + extension.Length
            || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !name.AsSpan(prefix.Length, name.Length - prefix.Length - extension.Length).ContainsAnyExceptInRange('0', '9');
    }

    private string? Source(IDictionary<string, object?> arguments)
    {
        if (Text(arguments, "path") is not { } path || workspace.Root is null)
        {
            return null;
        }

        try
        {
            var full = WorkspacePaths.Resolve(workspace, path);
            return DiagramFiles.CanContainDiagrams(full) ? full : null;
        }
        catch (AgentToolException)
        {
            return null;
        }
    }

    private static int? Block(IDictionary<string, object?> arguments)
    {
        try
        {
            return ToolArguments.GetOptional<int?>(arguments, "block");
        }
        catch (AgentToolException)
        {
            return null;
        }
    }

    private static string? Text(IDictionary<string, object?> arguments, string name) =>
        arguments.TryGetValue(name, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;
}
