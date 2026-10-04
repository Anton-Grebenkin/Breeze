using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Explorer.Resources;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>
/// Explorer operations as in VS Code: new file and folder, inline rename (open tabs follow the file), confirmed delete
/// to the recycle bin, copying paths. Errors go to the status bar without ending the edit.
/// </summary>
public sealed class ExplorerEditor(
    ExplorerViewModel explorer,
    IFileSystem fileSystem,
    IWorkspace workspace,
    IDialogService dialogs,
    ISystemShell systemShell,
    ICommandService commands,
    StatusBarViewModel statusBar,
    EditorTabRelocator tabs)
{
    private FileNodeViewModel? _editing;

    public FileNodeViewModel? EditingNode => _editing;

    public void BeginRename(FileNodeViewModel? node = null)
    {
        node ??= explorer.Selected;
        if (node is null || node.IsPlaceholder || ReferenceEquals(node, explorer.Tree?.Root))
        {
            return;
        }

        StartEditing(node, node.Name);
    }

    /// <summary>Inserts an unnamed node into the target folder and starts name entry.</summary>
    public async Task BeginCreateAsync(bool isDirectory)
    {
        if (explorer.Tree is not { } tree || explorer.TargetFolder is not { } folder)
        {
            return;
        }

        CancelEditing();
        if (!folder.IsChildrenLoaded)
        {
            await tree.LoadChildrenAsync(folder);
        }

        folder.IsExpanded = true;
        var pending = FileNodeViewModel.CreatePending(tree, folder, isDirectory);
        tree.Insert(folder, pending);
        StartEditing(pending, string.Empty);
    }

    /// <summary><c>Enter</c> or focus loss: creates or renames; <c>false</c> keeps editing an invalid name.</summary>
    public async Task<bool> CommitAsync()
    {
        if (_editing is not { } node || explorer.Tree is not { } tree || node.Parent is not { } parent)
        {
            return true;
        }

        var name = node.EditName.Trim();
        if (!node.IsPendingCreation && name == node.Name)
        {
            FinishEditing();
            return true;
        }

        var path = Path.Combine(parent.FullPath, name);
        if (Validate(node, name, path) is { } problem)
        {
            statusBar.Message = problem;
            return false;
        }

        try
        {
            if (node.IsPendingCreation)
            {
                await CreateAsync(tree, parent, node, path);
            }
            else
            {
                var oldPath = node.FullPath;
                fileSystem.Move(oldPath, path);
                tree.Relocate(node, path);
                FinishEditing();
                node.IsSelected = true;
                await tabs.FollowAsync(oldPath, path);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = exception.Message;
            return false;
        }
    }

    /// <summary><c>Esc</c>: removes a pending node or cancels the rename.</summary>
    public void CancelEditing()
    {
        if (_editing is not { } node)
        {
            return;
        }

        FinishEditing();
        if (node.IsPendingCreation)
        {
            explorer.Tree?.Remove(node);
        }
    }

    /// <summary>Moves to the recycle bin after confirmation; returns whether it was deleted.</summary>
    public bool Delete(FileNodeViewModel? node = null)
    {
        node ??= explorer.Selected;
        if (node is null || node.IsPlaceholder || explorer.Tree is not { } tree || ReferenceEquals(node, tree.Root))
        {
            return false;
        }

        var question = Format(node.IsDirectory ? Strings.DeleteFolderQuestion : Strings.DeleteFileQuestion, node.Name);
        if (!dialogs.Confirm(question, Strings.DeleteRestoreHint, Strings.Delete))
        {
            return false;
        }

        try
        {
            fileSystem.DeleteToRecycleBin(node.FullPath);
            tree.Remove(node);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = exception.Message;
            return false;
        }
    }

    public void CopyPath(bool relative)
    {
        if (explorer.Selected is { IsPlaceholder: false } node)
        {
            systemShell.CopyToClipboard(relative ? workspace.RelativePath(node.FullPath) : node.FullPath);
        }
    }

    public void RevealInFileManager()
    {
        if (explorer.Selected is { IsPlaceholder: false } node)
        {
            systemShell.RevealInFileManager(node.FullPath);
        }
    }

    /// <summary>Windows naming rules: no invalid chars, no trailing dot, no clash with a sibling.</summary>
    private string? Validate(FileNodeViewModel node, string name, string path)
    {
        if (name.Length == 0)
        {
            return Strings.NameRequired;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or ".." || name.EndsWith('.'))
        {
            return Format(Strings.NameInvalid, name);
        }

        var caseOnlyRename = !node.IsPendingCreation && string.Equals(path, node.FullPath, StringComparison.OrdinalIgnoreCase);
        return !caseOnlyRename && (fileSystem.FileExists(path) || fileSystem.DirectoryExists(path))
            ? Format(Strings.NameExists, name)
            : null;
    }

    private static string Format(string format, string argument) =>
        string.Format(CultureInfo.CurrentCulture, format, argument);

    private async Task CreateAsync(FileTree tree, FileNodeViewModel parent, FileNodeViewModel pending, string path)
    {
        var isDirectory = pending.IsDirectory;
        if (isDirectory)
        {
            fileSystem.CreateDirectory(path);
        }
        else
        {
            fileSystem.CreateFile(path);
        }

        FinishEditing();
        tree.Remove(pending);
        tree.AddCreated(parent, path, isDirectory).IsSelected = true;

        if (!isDirectory)
        {
            await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        }
    }

    private void StartEditing(FileNodeViewModel node, string initialName)
    {
        CancelEditing();
        _editing = node;
        node.EditName = initialName;
        node.IsEditing = true;
        explorer.SetEditingContext(true);
    }

    private void FinishEditing()
    {
        if (_editing is not null)
        {
            _editing.IsEditing = false;
            _editing = null;
        }

        explorer.SetEditingContext(false);
    }
}
