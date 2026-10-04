using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>
/// Explorer: the open folder's tree, selection and opening files. File operations and name editing live in
/// <see cref="ExplorerEditor"/>. Watcher events arrive on a background thread and are posted to the UI thread.
/// </summary>
public sealed partial class ExplorerViewModel : ObservableObject, IDisposable
{
    public const string FocusContextKey = "explorerFocus";
    public const string EditingContextKey = "explorerEditing";
    public const string FolderSelectedContextKey = "explorerResourceIsFolder";

    private readonly IWorkspace _workspace;
    private readonly IFileSystem _fileSystem;
    private readonly ICommandService _commands;
    private readonly IContextKeyService _context;
    private readonly IUiDispatcher _dispatcher;
    private readonly AgentChangeMarks? _marks;

    public ExplorerViewModel(
        IWorkspace workspace,
        IFileSystem fileSystem,
        ICommandService commands,
        IContextKeyService context,
        IUiDispatcher dispatcher,
        AgentChangeMarks? marks = null)
    {
        _workspace = workspace;
        _fileSystem = fileSystem;
        _commands = commands;
        _context = context;
        _dispatcher = dispatcher;
        _marks = marks;
        if (_marks is not null)
        {
            _marks.Changed += OnMarksChanged;
        }

        _workspace.Changed += OnWorkspaceChanged;
        _workspace.FilesChanged += OnFilesChanged;
        Rebuild();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkspace), nameof(FolderName))]
    public partial FileTree? Tree { get; private set; }

    public bool HasWorkspace => Tree is not null;

    public string FolderName => _workspace.Name ?? string.Empty;

    /// <summary>Root folder load; tests await it, the view just shows the tree.</summary>
    public Task RootLoaded { get; private set; } = Task.CompletedTask;

    public FileNodeViewModel? Selected => Tree?.Selected;

    /// <summary>Tree focus; gates <c>F2</c>, <c>Del</c> and <c>Enter</c>.</summary>
    public bool IsFocused
    {
        get;
        set
        {
            field = value;
            _context.Set(FocusContextKey, value);
        }
    }

    /// <summary>Folder for new items: the selected folder, the selected file's folder, or the root.</summary>
    public FileNodeViewModel? TargetFolder => Selected switch
    {
        { IsDirectory: true } folder => folder,
        { Parent: { } parent } => parent,
        _ => Tree?.Root,
    };

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _workspace.FilesChanged -= OnFilesChanged;
        if (_marks is not null)
        {
            _marks.Changed -= OnMarksChanged;
        }
    }

    /// <summary>Opens a file (optionally as a preview, like a VS Code single click) or toggles a folder.</summary>
    public async Task OpenAsync(FileNodeViewModel? node, bool preview = false)
    {
        node ??= Selected;
        if (node is null || node.IsPlaceholder || node.IsEditing)
        {
            return;
        }

        if (node.IsDirectory)
        {
            node.IsExpanded = !node.IsExpanded;
            return;
        }

        await _commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(node.FullPath, preview));
    }

    /// <summary>Selects a file in the tree, expanding folders on its path (follows the active editor tab).</summary>
    public async Task RevealAsync(string? filePath)
    {
        if (Tree is not { } tree || filePath is null || _workspace.IsExcluded(filePath, isDirectory: false))
        {
            return;
        }

        var folders = new Stack<string>();
        for (var folder = Path.GetDirectoryName(filePath); folder is not null && !tree.TryGet(folder, out _); folder = Path.GetDirectoryName(folder))
        {
            folders.Push(folder);
        }

        var parentPath = Path.GetDirectoryName(filePath)!;
        await EnsureExpandedAsync(tree, folders.Count > 0 ? Path.GetDirectoryName(folders.Peek())! : parentPath);
        while (folders.Count > 0)
        {
            await EnsureExpandedAsync(tree, folders.Pop());
        }

        if (tree.TryGet(filePath, out var node))
        {
            node.IsSelected = true;
        }
    }

    private static async Task EnsureExpandedAsync(FileTree tree, string folderPath)
    {
        if (!tree.TryGet(folderPath, out var folder))
        {
            return;
        }

        if (!folder.IsChildrenLoaded)
        {
            await tree.LoadChildrenAsync(folder);
        }

        if (!ReferenceEquals(folder, tree.Root))
        {
            folder.IsExpanded = true;
        }
    }

    [RelayCommand]
    public void CollapseAll() => Tree?.CollapseAll();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (Tree is not null)
        {
            await Tree.ApplyChangesAsync(new FileChangesEventArgs([], requiresRescan: true));
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync() => await _commands.ExecuteAsync(WorkspaceCommands.OpenFolderId);

    internal void SetEditingContext(bool isEditing) => _context.Set(EditingContextKey, isEditing);

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Rebuild();

    private void OnMarksChanged(object? sender, EventArgs e) => _dispatcher.Post(() => Tree?.ApplyMarks());

    private void OnFilesChanged(object? sender, FileChangesEventArgs e) =>
        _dispatcher.Post(() => _ = Tree?.ApplyChangesAsync(e));

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Selected));
        _context.Set(FolderSelectedContextKey, Selected?.IsDirectory ?? false);
    }

    private void Rebuild()
    {
        if (Tree is not null)
        {
            Tree.SelectionChanged -= OnSelectionChanged;
        }

        Tree = _workspace.Root is { } root ? new FileTree(_fileSystem, _workspace, _dispatcher, root) { Marks = _marks } : null;
        if (Tree is not null)
        {
            Tree.ApplyMarks();
            Tree.SelectionChanged += OnSelectionChanged;
            RootLoaded = Tree.LoadChildrenAsync(Tree.Root);
        }
    }
}
