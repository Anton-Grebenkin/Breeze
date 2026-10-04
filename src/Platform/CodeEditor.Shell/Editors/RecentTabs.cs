namespace CodeEditor.Shell.Editors;

/// <summary>Tabs in most-recently-used order, for <c>Ctrl+Tab</c> and for picking the next tab after closing one.</summary>
internal sealed class RecentTabs
{
    private readonly List<EditorTab> _tabs = [];

    /// <summary>The most recently used tab, or <c>null</c> if there are none.</summary>
    public EditorTab? First => _tabs.FirstOrDefault();

    /// <summary>The tab used before the current one, or <c>null</c> if there is only one.</summary>
    public EditorTab? Previous => _tabs.Count > 1 ? _tabs[1] : null;

    public void Touch(EditorTab tab)
    {
        _tabs.Remove(tab);
        _tabs.Insert(0, tab);
    }

    public void Forget(EditorTab tab) => _tabs.Remove(tab);

    public EditorTab? MostRecentIn(EditorGroupViewModel group) => _tabs.FirstOrDefault(group.Tabs.Contains);
}
