using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Editor tab group (ADR 0031) with its own tab strip and active tab. Groups sit side by side, like "Split Editor"
/// in VS Code.
/// </summary>
public sealed partial class EditorGroupViewModel : ObservableObject
{
    public ObservableCollection<EditorTab> Tabs { get; } = [];

    /// <summary>The tab shown in this group.</summary>
    [ObservableProperty]
    public partial EditorTab? Active { get; set; }

    /// <summary>The group the user works in; files open here.</summary>
    [ObservableProperty]
    public partial bool IsActiveGroup { get; set; }

    /// <summary>1-based position from the left, matching <c>Ctrl+1</c>, <c>Ctrl+2</c>…</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationId))]
    public partial int Number { get; set; } = 1;

    public string AutomationId => $"EditorGroup.{Number}";

    partial void OnActiveChanged(EditorTab? oldValue, EditorTab? newValue)
    {
        oldValue?.IsShown = false;
        newValue?.IsShown = true;
    }

    /// <summary>Where a new tab goes: in place of the preview tab, otherwise right after the active one.</summary>
    /// <returns>The index and the preview tab to replace, if any.</returns>
    public (int Index, EditorTab? Replaced) PlaceFor(EditorTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var replaced = tab.IsPreview ? Tabs.FirstOrDefault(candidate => candidate.IsPreview && !candidate.IsDirty) : null;
        var index = replaced is not null ? Tabs.IndexOf(replaced) : Active is null ? Tabs.Count : Tabs.IndexOf(Active) + 1;
        return (index, replaced);
    }
}
