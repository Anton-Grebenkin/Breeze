using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// Opens a link clicked in a reply: a project file in the editor at the given line, a web address in the browser.
/// Models often name a file without its folder (<c>Program.cs</c>, <c>Orders/Order.cs:12</c>): a path missing from the
/// workspace root is looked up in the file index by its ending, as VS Code does for terminal links; several matches
/// are offered in a quick pick, none is reported in the status bar.
/// </summary>
public sealed class ChatLinkOpener(
    IWorkspace workspace,
    IFileSystem fileSystem,
    IFileIndex index,
    IQuickPick quickPick,
    ICommandService commands,
    ISystemShell shell,
    StatusBarViewModel statusBar)
{
    private const int MaxMatches = 50;
    private const string CurrentFolderPrefix = "./";

    public async Task OpenAsync(string target)
    {
        var link = ChatLink.Parse(target);
        if (link.Web is { } uri)
        {
            shell.OpenInBrowser(uri);
            return;
        }

        var matches = await FindAsync(link.FilePath!);
        switch (matches.Count)
        {
            case 0:
                statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.FileNotFound, link.FilePath);
                break;
            case 1:
                await OpenAsync(matches[0].FullPath, link);
                break;
            default:
                var items = matches.Select(file => new QuickPickItem(file.FullPath, file.RelativePath)).ToList();
                quickPick.Show(new QuickPickProvider(
                    string.Format(CultureInfo.CurrentCulture, Strings.PickLinkedFile, link.FilePath), items, item => OpenAsync(item.Id, link)));
                break;
        }
    }

    private async Task OpenAsync(string path, ChatLink link)
    {
        await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        if (link.Line > 0)
        {
            await commands.ExecuteAsync(ShellCommandIds.EditorGoToLine, new EditorLocation(link.Line, Math.Max(link.Column, 1)));
        }
    }

    private async Task<IReadOnlyList<IndexedFile>> FindAsync(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.IsPathRooted(normalized)
            ? normalized
            : workspace.Root is { } root ? Path.Combine(root, normalized) : null;
        if (full is not null && fileSystem.FileExists(full))
        {
            var fullPath = Path.GetFullPath(full);
            return [new IndexedFile(fullPath, path, Path.GetFileName(fullPath))];
        }

        if (Path.IsPathRooted(normalized) || workspace.Root is null)
        {
            return [];
        }

        // The first scan may still run right after the folder opens. O(n) over the index; the shortest paths first.
        await index.WhenReady;
        var ending = "/" + TrimCurrentFolder(path.Replace('\\', '/'));
        return [.. index.Files
            .Where(file => ("/" + file.RelativePath).EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.RelativePath.Length)
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxMatches)];
    }

    private static string TrimCurrentFolder(string path) =>
        path.StartsWith(CurrentFolderPrefix, StringComparison.Ordinal) ? path[CurrentFolderPrefix.Length..] : path;
}
