using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Explorer.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Explorer.Services;

/// <summary>
/// Agent file deletion and moves, approval required. Deletion goes to the recycle bin, so a model mistake can be
/// undone. Open tabs learn about changes through the folder watcher.
/// </summary>
public sealed class ExplorerEditingTools(IWorkspace workspace, IFileSystem fileSystem, IAgentFileState fileState) : IAgentToolProvider, IAgentChangePreviewer
{
    public const string DeleteFileName = "delete_file";
    public const string MoveFileName = "move_file";

    public IEnumerable<AITool> CreateTools() =>
    [
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(DeleteFile, DeleteFileName,
            "Moves a workspace file or folder to the recycle bin. Requires user approval.")),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(MoveFile, MoveFileName,
            "Moves or renames a workspace file or folder. Fails if the destination exists. Requires user approval.")),
    ];

    public bool CanPreview(string toolName) => toolName is DeleteFileName or MoveFileName;

    public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        if (toolName == DeleteFileName)
        {
            var path = ExistingPath(ToolArguments.Get<string>(arguments, "path"));
            return Task.FromResult<IReadOnlyList<FileChangePreview>>([new FileChangePreview(ProposedChangeKind.Delete, workspace.RelativePath(path), TextOf(path), string.Empty)]);
        }

        var (from, to) = MovePaths(ToolArguments.Get<string>(arguments, "from"), ToolArguments.Get<string>(arguments, "to"));
        return Task.FromResult<IReadOnlyList<FileChangePreview>>(
            [new FileChangePreview(ProposedChangeKind.Move, workspace.RelativePath(from), string.Empty, string.Empty) { NewRelativePath = workspace.RelativePath(to) }]);
    }

    private string DeleteFile([Description("File or folder path relative to the workspace root.")] string path)
    {
        var full = ExistingPath(path);
        var previous = fileSystem.FileExists(full) ? TextOf(full) : null;
        fileSystem.DeleteToRecycleBin(full);
        if (previous is not null)
        {
            fileState.RecordWrite(full, text: null, previous);
        }

        return Format(Strings.ToolMovedToRecycleBin, workspace.RelativePath(full));
    }

    private string MoveFile(
        [Description("Current path relative to the workspace root.")] string from,
        [Description("New path relative to the workspace root.")] string to)
    {
        var (source, destination) = MovePaths(from, to);
        var folder = Path.GetDirectoryName(destination)!;
        if (!fileSystem.DirectoryExists(folder))
        {
            fileSystem.CreateDirectory(folder);
        }

        var text = fileSystem.FileExists(source) ? TextFileCodec.Decode(fileSystem.ReadAllBytes(source))?.Text : null;
        fileSystem.Move(source, destination);
        if (text is not null)
        {
            // Recorded as delete + create, so reverting the chat's changes moves the file back.
            fileState.RecordWrite(source, text: null, text);
            fileState.RecordWrite(destination, text, previousText: null);
        }

        return $"{workspace.RelativePath(source)} → {workspace.RelativePath(destination)}.";
    }

    private string ExistingPath(string path)
    {
        var full = WorkspacePaths.Resolve(workspace, path);
        if (string.Equals(full, workspace.Root, StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentToolException(Strings.ToolWorkspaceUntouchable);
        }

        SensitivePaths.EnsureWritable(workspace.RelativePath(full));

        return fileSystem.FileExists(full) || fileSystem.DirectoryExists(full) ? full : throw new AgentToolException(Format(Strings.ToolPathNotFound, path));
    }

    private (string From, string To) MovePaths(string from, string to)
    {
        var source = ExistingPath(from);
        var destination = WorkspacePaths.Resolve(workspace, to);
        SensitivePaths.EnsureWritable(workspace.RelativePath(destination));
        return fileSystem.FileExists(destination) || fileSystem.DirectoryExists(destination)
            ? throw new AgentToolException(Format(Strings.ToolPathExists, to))
            : (source, destination);
    }

    private static string Format(string format, string argument) =>
        string.Format(CultureInfo.CurrentCulture, format, argument);

    // File text for the delete card, showing what will be lost; empty for folders and binary files.
    private string TextOf(string path) =>
        fileSystem.FileExists(path) ? TextFileCodec.Decode(fileSystem.ReadAllBytes(path))?.Text ?? string.Empty : string.Empty;
}
