using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// A panel group: "Merge Changes", "Staged Changes" or "Changes". A new snapshot is merged into the existing rows in
/// place, so selection and scrolling survive refreshes. At most <see cref="MaxItems"/> rows are shown.
/// </summary>
public sealed partial class GitChangeGroupViewModel : ObservableObject
{
    /// <summary>Row limit per group; "stage all" and commit still take every file.</summary>
    public const int MaxItems = 5_000;

    // At this many differences rows are rebuilt at once: one notification instead of thousands.
    private const int RebuildThreshold = 200;

    private readonly Action<object> _selected;

    internal GitChangeGroupViewModel(GitChangeGroup group, Action<object> selected)
    {
        Group = group;
        _selected = selected;
    }

    public GitChangeGroup Group { get; }

    public string Title => Group switch
    {
        GitChangeGroup.Merge => Strings.GroupMerge,
        GitChangeGroup.Staged => Strings.GroupStaged,
        _ => Strings.GroupChanges,
    };

    public bool IsStaged => Group == GitChangeGroup.Staged;

    /// <summary>All changes of the group, including those beyond the shown limit.</summary>
    public IReadOnlyList<GitFileChange> Changes { get; private set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GitChangeItemViewModel> Items { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText), nameof(ToolTip))]
    public partial int Count { get; private set; }

    public string CountText => Count.ToString("#,0", CultureInfo.CurrentCulture);

    /// <summary>Tooltip when not all files are shown.</summary>
    public string? ToolTip => Count > MaxItems ? string.Format(CultureInfo.CurrentCulture, Strings.GroupLimited, MaxItems, Count) : null;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string AutomationId => $"Git.Group.{Group}";

    /// <param name="changes">Group changes sorted by <see cref="GitPathComparer"/>.</param>
    internal void Update(IReadOnlyList<GitFileChange> changes)
    {
        Changes = changes;
        Count = changes.Count;
        var shown = changes.Count > MaxItems ? changes.Take(MaxItems).ToList() : changes;
        if (Items.Count == 0 || Math.Abs(Items.Count - shown.Count) >= RebuildThreshold)
        {
            Items = [.. shown.Select(change => new GitChangeItemViewModel(change, _selected))];
            return;
        }

        Merge(shown);
    }

    /// <summary>Removes gone rows, inserts new ones in place and updates the rest. O(n + m) comparisons.</summary>
    private void Merge(IReadOnlyList<GitFileChange> changes)
    {
        var index = 0;
        foreach (var change in changes)
        {
            while (index < Items.Count && GitPathComparer.Instance.Compare(Items[index].Path, change.Path) < 0)
            {
                Items.RemoveAt(index);
            }

            if (index < Items.Count && Items[index].Path == change.Path)
            {
                Items[index].Change = change;
            }
            else
            {
                Items.Insert(index, new GitChangeItemViewModel(change, _selected));
            }

            index++;
        }

        while (Items.Count > index)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _selected(this);
        }
    }

    public override string ToString() => Title;
}
