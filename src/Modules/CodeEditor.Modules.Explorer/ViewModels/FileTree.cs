using System.Collections.ObjectModel;
using CodeEditor.Core.Files;
using CodeEditor.Core.Text;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Explorer.Services;

namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>
/// Workspace file tree: a path → node index for loaded folders only, background loading of folder content and
/// targeted updates from watcher events. Methods that change the tree run on the UI thread.
/// </summary>
public sealed class FileTree
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspace _workspace;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, FileNodeViewModel> _index = new(StringComparer.OrdinalIgnoreCase);

    public FileTree(IFileSystem fileSystem, IWorkspace workspace, IUiDispatcher dispatcher, string rootPath)
    {
        _fileSystem = fileSystem;
        _workspace = workspace;
        _dispatcher = dispatcher;
        Root = new FileNodeViewModel(this, parent: null, rootPath, isDirectory: true);
        _index[rootPath] = Root;
    }

    public FileNodeViewModel Root { get; }

    public FileNodeViewModel? Selected { get; private set; }

    /// <summary>Highlighting of agent edits; new nodes take it when created.</summary>
    public AgentChangeMarks? Marks { get; init; }

    public event EventHandler? SelectionChanged;

    /// <summary>VS Code order: folders first, then natural name order.</summary>
    public static int CompareNodes(FileNodeViewModel left, FileNodeViewModel right) =>
        left.IsDirectory != right.IsDirectory
            ? left.IsDirectory ? -1 : 1
            : NaturalStringComparer.Instance.Compare(left.Name, right.Name);

    public bool TryGet(string fullPath, out FileNodeViewModel node) => _index.TryGetValue(fullPath, out node!);

    /// <summary>Updates agent edit highlighting of the loaded nodes. O(loaded nodes).</summary>
    public void ApplyMarks()
    {
        if (Marks is null)
        {
            return;
        }

        foreach (var node in _index.Values)
        {
            node.ApplyMarks(Marks);
        }
    }

    /// <summary>
    /// Reads and sorts folder content in the background, then replaces the collection on the UI thread in one
    /// assignment instead of thousands of notifications. An inaccessible folder stays empty.
    /// </summary>
    public async Task LoadChildrenAsync(FileNodeViewModel folder)
    {
        var entries = await Task.Run(() => ReadEntries(folder.FullPath)).ConfigureAwait(false);
        await _dispatcher.InvokeAsync(() => MergeChildren(folder, entries)).ConfigureAwait(false);
    }

    /// <summary>Adds a node for a just-created file or folder at its sorted position.</summary>
    public FileNodeViewModel AddCreated(FileNodeViewModel parent, string fullPath, bool isDirectory)
    {
        // The watcher may have reported the new file already; don't add a second node.
        if (_index.TryGetValue(fullPath, out var existing))
        {
            return existing;
        }

        var node = new FileNodeViewModel(this, parent, fullPath, isDirectory);
        if (isDirectory)
        {
            node.IsChildrenLoaded = true;
            node.Children = [];
        }

        Insert(parent, node);
        return node;
    }

    /// <summary>
    /// Shows a file or folder that appeared on disk (created elsewhere, moved or copied here) if its folder is loaded and
    /// the path isn't excluded. A folder comes unloaded and reads its content on expand.
    /// </summary>
    /// <returns>The node, also when it is already shown; <c>null</c> if it isn't shown.</returns>
    public FileNodeViewModel? AddExisting(string path)
    {
        if (_index.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var parentPath = Path.GetDirectoryName(path);
        if (parentPath is null || !_index.TryGetValue(parentPath, out var parent) || !parent.IsChildrenLoaded)
        {
            return null;
        }

        var isDirectory = _fileSystem.DirectoryExists(path);
        if (!isDirectory && !_fileSystem.FileExists(path) || _workspace.IsExcluded(path, isDirectory))
        {
            return null;
        }

        var node = new FileNodeViewModel(this, parent, path, isDirectory);
        Insert(parent, node);
        return node;
    }

    /// <summary>Applies a batch of watcher events to the loaded part of the tree.</summary>
    public async Task ApplyChangesAsync(FileChangesEventArgs changes)
    {
        if (changes.RequiresRescan)
        {
            await ReloadLoadedFoldersAsync();
            return;
        }

        foreach (var change in changes.Changes)
        {
            if (change.Kind == FileChangeKind.Deleted && _index.TryGetValue(change.Path, out var deleted))
            {
                Remove(deleted);
            }
            else if (change.Kind == FileChangeKind.Created)
            {
                AddExisting(change.Path);
            }
        }
    }

    public void Insert(FileNodeViewModel parent, FileNodeViewModel node)
    {
        var siblings = parent.Children;
        var position = 0;
        while (position < siblings.Count && CompareNodes(siblings[position], node) < 0)
        {
            position++;
        }

        siblings.Insert(position, node);
        if (!node.IsPendingCreation)
        {
            _index[node.FullPath] = node;
        }
    }

    public void Remove(FileNodeViewModel node)
    {
        node.Parent?.Children.Remove(node);
        RemoveDescendantsFromIndex(node);
        _index.Remove(node.FullPath);
        if (ReferenceEquals(Selected, node))
        {
            Selected = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Moves a renamed node to its new sorted position; folder content is re-read on expand.</summary>
    public void Relocate(FileNodeViewModel node, string newPath)
    {
        var parent = node.Parent!;
        parent.Children.Remove(node);
        _index.Remove(node.FullPath);
        RemoveDescendantsFromIndex(node);

        node.Relocate(newPath);
        if (node.IsDirectory)
        {
            node.Unload();
        }

        Insert(parent, node);
    }

    public void CollapseAll()
    {
        foreach (var node in _index.Values.Where(node => node.IsDirectory && !ReferenceEquals(node, Root)))
        {
            node.IsExpanded = false;
        }
    }

    internal void OnSelected(FileNodeViewModel node)
    {
        Selected = node;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReloadLoadedFoldersAsync()
    {
        var loaded = _index.Values.Where(node => node.IsDirectory && node.IsChildrenLoaded).ToArray();
        foreach (var folder in loaded.Where(folder => ReferenceEquals(folder, Root) || folder.IsExpanded))
        {
            await LoadChildrenAsync(folder);
        }
    }

    /// <summary>Background part of loading: read, exclude and sort. An inaccessible folder is empty.</summary>
    private List<FileSystemEntry> ReadEntries(string directory)
    {
        List<FileSystemEntry> entries;
        try
        {
            entries = [.. _fileSystem.EnumerateEntries(directory).Where(entry => !_workspace.IsExcluded(entry.FullPath, entry.IsDirectory))];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        entries.Sort((left, right) => left.IsDirectory != right.IsDirectory
            ? left.IsDirectory ? -1 : 1
            : NaturalStringComparer.Instance.Compare(left.Name, right.Name));
        return entries;
    }

    /// <summary>
    /// Merges with shown nodes: existing ones keep their state (expanded, selected, loaded), new ones are created,
    /// vanished ones are removed from the index with their descendants.
    /// </summary>
    private void MergeChildren(FileNodeViewModel folder, List<FileSystemEntry> entries)
    {
        var existing = folder.Children
            .Where(child => !child.IsPlaceholder && !child.IsPendingCreation)
            .ToDictionary(child => child.FullPath, StringComparer.OrdinalIgnoreCase);

        var children = new List<FileNodeViewModel>(entries.Count);
        foreach (var entry in entries)
        {
            if (existing.Remove(entry.FullPath, out var kept) && kept.IsDirectory == entry.IsDirectory)
            {
                children.Add(kept);
                continue;
            }

            var node = new FileNodeViewModel(this, folder, entry.FullPath, entry.IsDirectory);
            _index[node.FullPath] = node;
            children.Add(node);
        }

        foreach (var removed in existing.Values)
        {
            RemoveDescendantsFromIndex(removed);
            _index.Remove(removed.FullPath);
        }

        folder.Children = new ObservableCollection<FileNodeViewModel>(children);
        folder.IsChildrenLoaded = true;
    }

    private void RemoveDescendantsFromIndex(FileNodeViewModel folder)
    {
        foreach (var child in folder.Children.Where(child => !child.IsPlaceholder))
        {
            RemoveDescendantsFromIndex(child);
            _index.Remove(child.FullPath);
        }
    }
}
