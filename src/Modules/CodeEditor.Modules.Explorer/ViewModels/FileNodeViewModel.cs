using System.Collections.ObjectModel;
using CodeEditor.Modules.Explorer.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>
/// File tree node. An unloaded folder has a single placeholder child so the tree shows an expander;
/// the real content loads on expand.
/// </summary>
public sealed partial class FileNodeViewModel : ObservableObject
{
    private readonly FileTree? _tree;

    internal FileNodeViewModel(FileTree? tree, FileNodeViewModel? parent, string fullPath, bool isDirectory)
    {
        _tree = tree;
        Parent = parent;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Name = Path.GetFileName(fullPath);
        Children = isDirectory && tree is not null ? [CreatePlaceholder(this)] : [];
        if (tree?.Marks is { } marks)
        {
            ApplyMarks(marks);
        }
    }

    public FileNodeViewModel? Parent { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon), nameof(AutomationId))]
    public partial string Name { get; private set; }

    public string FullPath { get; private set; }

    public bool IsDirectory { get; }

    /// <summary>Placeholder child of an unloaded folder; not a real file.</summary>
    public bool IsPlaceholder => _tree is null;

    public bool IsChildrenLoaded { get; internal set; }

    [ObservableProperty]
    public partial ObservableCollection<FileNodeViewModel> Children { get; internal set; }

    public string Icon => IsDirectory
        ? IsExpanded ? FileIcons.FolderOpen : FileIcons.FolderClosed
        : FileIcons.ForFile(Name);

    public string AutomationId => $"Explorer.Node.{Name}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon))]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The name is being edited inline (rename or new item).</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; internal set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    /// <summary>The file has agent edits awaiting review (ADR 0040).</summary>
    [ObservableProperty]
    public partial bool IsAgentChanged { get; private set; }

    /// <summary>The folder contains files with agent edits awaiting review.</summary>
    [ObservableProperty]
    public partial bool ContainsAgentChanges { get; private set; }

    /// <summary>A new item not yet created on disk; cancelling the edit removes the node.</summary>
    public bool IsPendingCreation { get; internal set; }

    internal static FileNodeViewModel CreatePending(FileTree tree, FileNodeViewModel parent, bool isDirectory) =>
        new(tree, parent, parent.FullPath + Path.DirectorySeparatorChar, isDirectory)
        {
            IsPendingCreation = true,
            IsChildrenLoaded = true,
            Children = [],
        };

    internal void ApplyMarks(AgentChangeMarks marks)
    {
        IsAgentChanged = !IsDirectory && marks.IsChanged(FullPath);
        ContainsAgentChanges = IsDirectory && marks.Contains(FullPath);
    }

    internal void Relocate(string fullPath)
    {
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
    }

    /// <summary>Drops the loaded folder content; it is re-read on the next expand.</summary>
    internal void Unload()
    {
        IsExpanded = false;
        IsChildrenLoaded = false;
        Children = [CreatePlaceholder(this)];
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !IsChildrenLoaded && _tree is not null)
        {
            _ = _tree.LoadChildrenAsync(this);
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _tree?.OnSelected(this);
        }
    }

    private static FileNodeViewModel CreatePlaceholder(FileNodeViewModel parent) =>
        new(tree: null, parent, Path.Combine(parent.FullPath, "…"), isDirectory: false);

    public override string ToString() => Name;
}
