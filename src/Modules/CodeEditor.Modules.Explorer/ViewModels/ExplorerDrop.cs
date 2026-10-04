using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Explorer.Resources;
using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>
/// Dropping files onto the explorer tree, as in VS Code: onto a folder row, a file row (its folder) or empty space (the
/// root). Explorer items move, or copy with <c>Ctrl</c>; files from Windows are copied. A name clash asks whether to
/// replace (the replaced item goes to the Recycle Bin), otherwise the item is skipped; a copy into its own folder gets
/// a free name. Open tabs follow moved files. Disk work runs in the background, the tree updates on the UI thread.
/// </summary>
public sealed class ExplorerDrop(
    ExplorerViewModel explorer,
    IFileSystem fileSystem,
    IDialogService dialogs,
    StatusBarViewModel statusBar,
    EditorTabRelocator tabs)
{
    /// <summary>The folder a drop onto the node lands in: the folder itself, a file's folder, the root for empty space.</summary>
    public FileNodeViewModel? TargetOf(FileNodeViewModel? node) => node switch
    {
        { IsDirectory: true, IsPlaceholder: false, IsPendingCreation: false } folder => folder,
        { Parent: { } parent } => parent,
        _ => explorer.Tree?.Root,
    };

    /// <summary>
    /// What dropping the paths onto the node would do. Refused: a folder into itself or its descendant, a move into
    /// the folder the items are already in. Only paths are compared, without the disk: it runs on every mouse move.
    /// </summary>
    public ExplorerDropEffect Evaluate(IReadOnlyList<string> sources, FileNodeViewModel? node, ExplorerDropEffect requested)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (requested == ExplorerDropEffect.None || sources.Count == 0 || TargetOf(node) is not { } target)
        {
            return ExplorerDropEffect.None;
        }

        var folder = target.FullPath;
        if (sources.Any(source => IsSameOrInside(folder, source)))
        {
            return ExplorerDropEffect.None;
        }

        var alreadyThere = sources.All(source => string.Equals(Path.GetDirectoryName(source), folder, StringComparison.OrdinalIgnoreCase));
        return requested == ExplorerDropEffect.Move && alreadyThere ? ExplorerDropEffect.None : requested;
    }

    /// <summary>Moves or copies the items into the target folder, then expands it and selects the last item.</summary>
    public async Task DropAsync(IReadOnlyList<string> sources, FileNodeViewModel? node, ExplorerDropEffect effect)
    {
        if (Evaluate(sources, node, effect) == ExplorerDropEffect.None || explorer.Tree is not { } tree || TargetOf(node) is not { } target)
        {
            return;
        }

        if (!target.IsChildrenLoaded)
        {
            await tree.LoadChildrenAsync(target);
        }

        FileNodeViewModel? last = null;
        foreach (var source in sources)
        {
            last = await TransferAsync(tree, source, target, effect) ?? last;
        }

        if (!ReferenceEquals(target, tree.Root))
        {
            target.IsExpanded = true;
        }

        last?.IsSelected = true;
    }

    private async Task<FileNodeViewModel?> TransferAsync(FileTree tree, string source, FileNodeViewModel target, ExplorerDropEffect effect)
    {
        var isDirectory = fileSystem.DirectoryExists(source);
        if (!isDirectory && !fileSystem.FileExists(source))
        {
            return null;
        }

        if (Plan(source, target.FullPath, isDirectory, effect) is not { } plan)
        {
            return null;
        }

        try
        {
            if (plan.Replaces)
            {
                fileSystem.DeleteToRecycleBin(plan.Destination);
                Forget(tree, plan.Destination);
            }

            await Task.Run(() => Transfer(source, plan.Destination, isDirectory, effect));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = exception.Message;
            return null;
        }

        if (effect == ExplorerDropEffect.Move)
        {
            Forget(tree, source);
            await tabs.FollowAsync(source, plan.Destination);
        }

        return tree.AddExisting(plan.Destination);
    }

    /// <returns>Where the item goes and whether it replaces an existing one; <c>null</c> to skip the item.</returns>
    private DropPlan? Plan(string source, string folder, bool isDirectory, ExplorerDropEffect effect)
    {
        var name = Path.GetFileName(source);
        var destination = Path.Combine(folder, name);
        if (string.Equals(destination, source, StringComparison.OrdinalIgnoreCase))
        {
            return effect == ExplorerDropEffect.Copy ? new DropPlan(CopyNames.NextFree(fileSystem, folder, name, isDirectory), Replaces: false) : null;
        }

        if (!fileSystem.FileExists(destination) && !fileSystem.DirectoryExists(destination))
        {
            return new DropPlan(destination, Replaces: false);
        }

        return ConfirmReplace(source, destination) ? new DropPlan(destination, Replaces: true) : null;
    }

    private bool ConfirmReplace(string source, string destination)
    {
        var name = Path.GetFileName(destination);
        if (IsSameOrInside(source, destination))
        {
            statusBar.Message = Format(Strings.CannotReplaceParent, name);
            return false;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(destination)!);
        return dialogs.Confirm(Format(Strings.ReplaceQuestion, name, folder), Strings.ReplaceHint, Strings.Replace);
    }

    private void Transfer(string source, string destination, bool isDirectory, ExplorerDropEffect effect)
    {
        if (effect == ExplorerDropEffect.Move)
        {
            fileSystem.Move(source, destination);
        }
        else if (isDirectory)
        {
            CopyFolder(source, destination);
        }
        else
        {
            fileSystem.CopyFile(source, destination);
        }
    }

    // Everything is copied, excluded folders too, as Windows does; recursion depth is the folder depth.
    private void CopyFolder(string source, string destination)
    {
        fileSystem.CreateDirectory(destination);
        foreach (var entry in fileSystem.EnumerateEntries(source).ToList())
        {
            var target = Path.Combine(destination, entry.Name);
            if (entry.IsDirectory)
            {
                CopyFolder(entry.FullPath, target);
            }
            else
            {
                fileSystem.CopyFile(entry.FullPath, target);
            }
        }
    }

    private static void Forget(FileTree tree, string path)
    {
        if (tree.TryGet(path, out var node))
        {
            tree.Remove(node);
        }
    }

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Format(string format, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, format, arguments);

    private readonly record struct DropPlan(string Destination, bool Replaces);
}
