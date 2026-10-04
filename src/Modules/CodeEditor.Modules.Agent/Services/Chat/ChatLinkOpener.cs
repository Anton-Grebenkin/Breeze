using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// Opens a link clicked in a reply: a project file in the editor at the given line, a web address in the browser.
/// Relative paths resolve against the workspace folder; a missing file is reported in the status bar.
/// </summary>
public sealed class ChatLinkOpener(IWorkspace workspace, IFileSystem fileSystem, ICommandService commands, ISystemShell shell, StatusBarViewModel statusBar)
{
    public async Task OpenAsync(string target)
    {
        var link = ChatLink.Parse(target);
        if (link.Web is { } uri)
        {
            shell.OpenInBrowser(uri);
            return;
        }

        if (Resolve(link.FilePath!) is not { } path)
        {
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.FileNotFound, link.FilePath);
            return;
        }

        await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        if (link.Line > 0)
        {
            await commands.ExecuteAsync(ShellCommandIds.EditorGoToLine, new EditorLocation(link.Line, Math.Max(link.Column, 1)));
        }
    }

    private string? Resolve(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.IsPathRooted(normalized)
            ? normalized
            : workspace.Root is { } root ? Path.Combine(root, normalized) : null;
        return full is not null && fileSystem.FileExists(full) ? Path.GetFullPath(full) : null;
    }
}
