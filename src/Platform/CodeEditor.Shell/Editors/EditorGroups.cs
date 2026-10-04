using System.Collections.ObjectModel;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Editor groups from left to right (ADR 0031): new ones go next to a neighbor, at most <see cref="Max"/>; empty ones
/// are removed except the last; numbered for <c>Ctrl+1</c>…<c>Ctrl+4</c>.
/// </summary>
public sealed class EditorGroups
{
    public const int Max = 4;

    public EditorGroups() => Items.Add(new EditorGroupViewModel());

    public ObservableCollection<EditorGroupViewModel> Items { get; } = [];

    public EditorGroupViewModel? GroupOf(EditorTab tab) => Items.FirstOrDefault(group => group.Tabs.Contains(tab));

    /// <summary>The group to the right, or <c>null</c>.</summary>
    public EditorGroupViewModel? RightOf(EditorGroupViewModel group)
    {
        var index = Items.IndexOf(group);
        return index >= 0 && index + 1 < Items.Count ? Items[index + 1] : null;
    }

    /// <summary>Adds an empty group next to a neighbor; <c>null</c> if there are already <see cref="Max"/>.</summary>
    public EditorGroupViewModel? Add(EditorGroupViewModel neighbor, bool before = false)
    {
        ArgumentNullException.ThrowIfNull(neighbor);
        if (Items.Count >= Max || !Items.Contains(neighbor))
        {
            return null;
        }

        var group = new EditorGroupViewModel();
        Items.Insert(Items.IndexOf(neighbor) + (before ? 0 : 1), group);
        Renumber();
        return group;
    }

    /// <summary>
    /// Moves a tab to a group, at an index or after the active tab; within one group it is reordered in place. If the
    /// tab was active in the source group, <paramref name="nextActive"/> picks the replacement.
    /// </summary>
    /// <returns>The source group, or <c>null</c> if nothing moved.</returns>
    public EditorGroupViewModel? Move(EditorTab tab, EditorGroupViewModel target, int? index, Func<EditorGroupViewModel, EditorTab?> nextActive)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(nextActive);
        if (GroupOf(tab) is not { } source || !Items.Contains(target))
        {
            return null;
        }

        if (ReferenceEquals(source, target))
        {
            var from = source.Tabs.IndexOf(tab);
            source.Tabs.Move(from, Math.Clamp(index is { } to && to > from ? to - 1 : index ?? from, 0, source.Tabs.Count - 1));
            return source;
        }

        source.Tabs.Remove(tab);
        if (ReferenceEquals(source.Active, tab))
        {
            source.Active = nextActive(source);
        }

        target.Tabs.Insert(index is { } requested ? Math.Clamp(requested, 0, target.Tabs.Count) : target.PlaceFor(tab).Index, tab);
        return source;
    }

    /// <summary>Removes an empty group unless it is the last one.</summary>
    /// <returns>The group to work in instead (the left neighbor); <c>null</c> if the group stayed.</returns>
    public EditorGroupViewModel? RemoveIfEmpty(EditorGroupViewModel group)
    {
        if (group.Tabs.Count > 0 || Items.Count == 1 || !Items.Contains(group))
        {
            return null;
        }

        var index = Items.IndexOf(group);
        Items.Remove(group);
        Renumber();
        return Items[Math.Max(0, index - 1)];
    }

    private void Renumber()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].Number = index + 1;
        }
    }
}
