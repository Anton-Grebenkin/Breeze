using System.Globalization;
using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Tools.Resources;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>New tool from the template: <c>tool.md</c> and <c>run.ps1</c> in <c>.breeze/tools/&lt;name&gt;</c>.</summary>
public sealed class ToolScaffold(IWorkspace workspace, IFileSystem fileSystem)
{
    public const string ScriptFile = "run.ps1";

    /// <returns>Full path of the new <c>tool.md</c>; <c>null</c> — no folder is open.</returns>
    /// <exception cref="IOException">The tool exists or the files could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is read-only.</exception>
    public string? Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (workspace.Root is not { } root)
        {
            return null;
        }

        var folder = Path.Combine(root, SettingsService.WorkspaceFolder, ToolShelf.FolderName, name);
        if (fileSystem.DirectoryExists(folder))
        {
            throw new IOException(string.Format(CultureInfo.CurrentCulture, Strings.ToolExists, name));
        }

        EnsureFolder(Path.Combine(root, SettingsService.WorkspaceFolder));
        EnsureFolder(Path.Combine(root, SettingsService.WorkspaceFolder, ToolShelf.FolderName));
        fileSystem.CreateDirectory(folder);
        var definition = Path.Combine(folder, ToolDefinition.DefinitionFile);
        Write(definition, Strings.ToolTemplateDefinition);
        Write(Path.Combine(folder, ScriptFile), Strings.ToolTemplateScript);
        return definition;
    }

    private void EnsureFolder(string folder)
    {
        if (!fileSystem.DirectoryExists(folder))
        {
            fileSystem.CreateDirectory(folder);
        }
    }

    private void Write(string path, string text) => fileSystem.WriteAllBytesAtomic(path, Encoding.UTF8.GetBytes(text.ReplaceLineEndings("\n")));
}
