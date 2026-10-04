using System.Collections.ObjectModel;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Tabbed editor area, as in VS Code: side-by-side groups (<see cref="EditorGroupViewModel"/>, ADR 0031), preview
/// tabs pinned on edit, most-recent order for <c>Ctrl+Tab</c>, reopening closed tabs, one save prompt for all dirty
/// files. A tab holds a text document, a file viewer (<see cref="IFileViewerProvider"/>) or a module view
/// (<see cref="EditorViews"/>). Empty groups close, except the last one.
/// </summary>
public sealed partial class EditorAreaViewModel : ObservableObject, IShutdownGuard
{
    public const string EditorOpenContextKey = "editorIsOpen";
    public const string ActiveDirtyContextKey = "activeEditorIsDirty";
    private readonly IDocumentService _documents;
    private readonly DocumentSaver _saver;
    private readonly IContextKeyService _context;
    private readonly EditorOpener _opener;
    private readonly RecentTabs _recent = new();
    private readonly ClosedTabs _closed = new();
    private readonly EditorGroups _groups = new();

    public EditorAreaViewModel(
        IDocumentService documents,
        IEnumerable<IEditorProvider> providers,
        IEnumerable<IFileViewerProvider> viewers,
        DocumentSaver saver,
        IContextKeyService context,
        StatusBarViewModel statusBar)
    {
        _documents = documents;
        _saver = saver;
        _context = context;
        _opener = new EditorOpener(this, new EditorTabFactory(documents, providers, viewers, statusBar));
        ActiveGroup = Groups[0];
    }

    /// <summary>Groups from left to right; there is always at least one.</summary>
    public ObservableCollection<EditorGroupViewModel> Groups => _groups.Items;

    [ObservableProperty]
    public partial EditorGroupViewModel ActiveGroup { get; private set; }

    /// <summary>All tabs, group by group from left to right.</summary>
    public IReadOnlyList<EditorTab> Tabs => [.. Groups.SelectMany(group => group.Tabs)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTabs), nameof(ActiveDocument))]
    public partial EditorTab? Active { get; private set; }

    /// <summary>The active tab's document; <c>null</c> if there are no tabs or the active one isn't text.</summary>
    public IDocument? ActiveDocument => (Active as EditorTabViewModel)?.Document;

    public bool HasTabs => Groups.Any(group => group.Tabs.Count > 0);

    public IReadOnlyList<string> RecentlyClosed => _closed.Items;

    /// <summary>Raised when the active tab changes or the last tab closes; the explorer selects the file.</summary>
    public event EventHandler? ActiveDocumentChanged;

    /// <summary>Opens a file in the active group with its viewer if one exists, otherwise as text.</summary>
    public Task<EditorTab?> OpenAsync(OpenFileRequest request) => _opener.OpenAsync(request, asText: false);

    /// <summary>Opens a file as text for editing, bypassing viewers.</summary>
    /// <returns>The document tab (it replaces a viewer of a text file such as CSV); <c>null</c> if the file couldn't
    /// be opened as text or is shown by a binary viewer.</returns>
    public async Task<EditorTabViewModel?> OpenTextAsync(OpenFileRequest request) => await _opener.OpenAsync(request, asText: true) as EditorTabViewModel;

    /// <summary>Finds a tab by key: a full file path or a view id.</summary>
    public EditorTab? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalized = Path.IsPathFullyQualified(key) ? Path.GetFullPath(key) : key;
        return Groups
            .SelectMany(group => group.Tabs)
            .FirstOrDefault(tab => string.Equals(tab.Key, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public EditorGroupViewModel? GroupOf(EditorTab tab) => _groups.GroupOf(tab);

    /// <summary>Inserts a tab after the group's active tab (active group by default); a preview replaces the old one.</summary>
    public EditorTab Insert(EditorTab tab, EditorGroupViewModel? group = null)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var target = group is not null && Groups.Contains(group) ? group : ActiveGroup;
        tab.PropertyChanged += (_, e) => OnTabPropertyChanged(tab, e.PropertyName);
        var (index, replaced) = target.PlaceFor(tab);
        target.Tabs.Insert(index, tab);
        if (replaced is not null)
        {
            Remove(replaced, rememberClosed: false);
        }

        Activate(tab);
        return tab;
    }

    [RelayCommand]
    public void Activate(EditorTab? tab)
    {
        if (tab is not null && GroupOf(tab) is { } group)
        {
            group.Active = tab;
            ActiveGroup = group;
            Active = tab;
        }
    }

    /// <summary>Makes the group active, so files open in it.</summary>
    public void Activate(EditorGroupViewModel group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Activate(group.Active);
        if (group.Active is null && Groups.Contains(group))
        {
            ActiveGroup = group;
        }
    }

    /// <summary>Adds an empty group next to a neighbor; <c>null</c> if there are already <see cref="EditorGroups.Max"/>.</summary>
    public EditorGroupViewModel? AddGroup(EditorGroupViewModel neighbor, bool before = false) => _groups.Add(neighbor, before);

    /// <summary>Moves a tab to a group (at an index or after its active tab) and activates it; an emptied group closes.</summary>
    public void MoveToGroup(EditorTab tab, EditorGroupViewModel target, int? index = null)
    {
        if (_groups.Move(tab, target, index, _recent.MostRecentIn) is { } source)
        {
            Activate(tab);
            RemoveIfEmpty(source);
        }
    }

    /// <summary>Closes empty groups, e.g. after restoring a session whose files were deleted.</summary>
    public void CloseEmptyGroups()
    {
        foreach (var group in Groups.Where(group => group.Tabs.Count == 0).ToList())
        {
            RemoveIfEmpty(group);
        }
    }

    /// <summary>The group right of the active one, for "open to the side"; created if missing and there is room.</summary>
    public EditorGroupViewModel SideGroup() => _groups.RightOf(ActiveGroup) ?? _groups.Add(ActiveGroup) ?? ActiveGroup;

    [RelayCommand]
    public async Task<bool> CloseAsync(EditorTab? tab) =>
        (tab ?? Active) is not { } target || await CloseManyAsync([target]);

    public Task<bool> CloseOthersAsync() => CloseManyAsync(ActiveGroup.Tabs.Where(tab => !ReferenceEquals(tab, Active)));

    public Task<bool> CloseToTheRightAsync() =>
        Active is null ? Task.FromResult(true) : CloseManyAsync(ActiveGroup.Tabs.Skip(ActiveGroup.Tabs.IndexOf(Active) + 1));

    public Task<bool> CloseAllAsync() => CloseManyAsync(Tabs);

    /// <summary>Closes tabs, asking once about dirty ones. Returns <c>false</c> if the user cancelled.</summary>
    public async Task<bool> CloseManyAsync(IEnumerable<EditorTab> tabs)
    {
        var closing = tabs.ToList();
        if (!await _saver.ResolveUnsavedAsync(closing))
        {
            return false;
        }

        foreach (var tab in closing)
        {
            Remove(tab, rememberClosed: true);
        }

        return true;
    }

    public async Task ReopenClosedAsync()
    {
        while (_closed.TakeLast() is { } path)
        {
            if (await OpenAsync(new OpenFileRequest(path)) is not null)
            {
                return;
            }
        }
    }

    /// <summary><c>Ctrl+PageDown</c> / <c>Ctrl+PageUp</c>: the neighboring tab of the active group, wrapping around.</summary>
    public void ActivateNeighbor(int delta)
    {
        var tabs = ActiveGroup.Tabs;
        if (Active is not null && tabs.Count > 1)
        {
            Activate(tabs[((tabs.IndexOf(Active) + delta) % tabs.Count + tabs.Count) % tabs.Count]);
        }
    }

    /// <summary><c>Ctrl+Tab</c>: the previously used tab, to switch quickly between two files.</summary>
    public void ActivatePreviousRecent() => Activate(_recent.Previous);

    public Task SaveActiveAsync() => ActiveDocument is { } document ? _saver.SaveAsync(document) : Task.CompletedTask;

    public Task SaveAllAsync() => _saver.SaveAllAsync(Tabs);

    public Task RevertActiveAsync() => ActiveDocument is { } document ? _documents.ReloadAsync(document) : Task.CompletedTask;

    public Task<bool> CanShutdownAsync() => _saver.ResolveUnsavedAsync(Tabs);

    private void Remove(EditorTab tab, bool rememberClosed)
    {
        if (GroupOf(tab) is not { } group)
        {
            return;
        }

        group.Tabs.Remove(tab);
        _recent.Forget(tab);
        tab.Dispose();
        if (tab is EditorTabViewModel { Document: var document })
        {
            _documents.Close(document);
        }

        if (rememberClosed && tab.FilePath is { } path)
        {
            _closed.Remember(path);
        }

        if (ReferenceEquals(group.Active, tab))
        {
            group.Active = _recent.MostRecentIn(group);
        }

        var wasActive = ReferenceEquals(Active, tab);
        RemoveIfEmpty(group);
        if (wasActive)
        {
            // As in VS Code: activate the group's most recently used tab, otherwise one from a neighboring group.
            Active = ActiveGroup.Active ?? _recent.First;
            Activate(Active);
        }

        UpdateContext();
    }

    private void RemoveIfEmpty(EditorGroupViewModel group)
    {
        if (_groups.RemoveIfEmpty(group) is { } fallback && ReferenceEquals(ActiveGroup, group))
        {
            ActiveGroup = fallback;
        }
    }

    partial void OnActiveGroupChanged(EditorGroupViewModel oldValue, EditorGroupViewModel newValue)
    {
        // The first assignment comes from the constructor, when there is no previous group.
        oldValue?.IsActiveGroup = false;
        newValue.IsActiveGroup = true;
    }

    partial void OnActiveChanged(EditorTab? oldValue, EditorTab? newValue)
    {
        oldValue?.IsActive = false;
        if (newValue is not null)
        {
            newValue.IsActive = true;
            _recent.Touch(newValue);
        }

        UpdateContext();
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTabPropertyChanged(EditorTab tab, string? propertyName)
    {
        if (ReferenceEquals(tab, Active) && propertyName == nameof(EditorTab.IsDirty))
        {
            UpdateContext();
        }
    }

    private void UpdateContext()
    {
        _context.Set(EditorOpenContextKey, HasTabs);
        _context.Set(ActiveDirtyContextKey, Active?.IsDirty ?? false);
        OnPropertyChanged(nameof(HasTabs));
    }
}
