using CodeEditor.Core.Context;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Tool windows in the editor area (ADR 0031) are shown as tabs. Closing the tab only hides the tool window: its
/// content stays alive and the show command reopens the tab. Moving it to another area closes the tab. The
/// <see cref="ActiveIsToolWindowContextKey"/> context key enables the "Move to…" items in the tab menu.
/// </summary>
public sealed class EditorToolWindows : IDisposable
{
    public const string ActiveIsToolWindowContextKey = "activeEditorIsToolWindow";

    private const string KeyPrefix = "toolwindow:";

    private readonly EditorAreaViewModel _editors;
    private readonly ToolWindowPlacement _placement;
    private readonly IContextKeyService _context;

    public EditorToolWindows(EditorAreaViewModel editors, ToolWindowPlacement placement, IContextKeyService context)
    {
        _editors = editors;
        _placement = placement;
        _context = context;
        _placement.Changed += OnPlacementChanged;
        _editors.ActiveDocumentChanged += OnActiveChanged;
    }

    /// <summary>The tool window in the active tab; <c>null</c> if the active tab isn't one.</summary>
    public ToolWindowViewModel? Active => _editors.Active?.Key is { } key && key.StartsWith(KeyPrefix, StringComparison.Ordinal)
        ? _placement.Find(key[KeyPrefix.Length..])
        : null;

    public static string KeyOf(string toolWindowId) => KeyPrefix + toolWindowId;

    /// <summary>Opens or activates the tool window's tab.</summary>
    public EditorTab Show(ToolWindowViewModel toolWindow)
    {
        ArgumentNullException.ThrowIfNull(toolWindow);
        if (_editors.Find(KeyOf(toolWindow.Id)) is { } open)
        {
            _editors.Activate(open);
            return open;
        }

        return _editors.Insert(new ViewTabViewModel(
            KeyOf(toolWindow.Id), toolWindow.Title, toolWindow.ToolTip, filePath: null, toolWindow.Content, isPreview: false, ownsEditor: false));
    }

    public void Dispose()
    {
        _placement.Changed -= OnPlacementChanged;
        _editors.ActiveDocumentChanged -= OnActiveChanged;
    }

    // A tool window moved out of the editor: close its tab, the new area shows the content.
    private void OnPlacementChanged(object? sender, EventArgs e)
    {
        foreach (var tab in _editors.Tabs.Where(IsMovedAway).ToList())
        {
            _ = _editors.CloseAsync(tab);
        }
    }

    private void OnActiveChanged(object? sender, EventArgs e) => _context.Set(ActiveIsToolWindowContextKey, Active is not null);

    private bool IsMovedAway(EditorTab tab) =>
        tab.Key.StartsWith(KeyPrefix, StringComparison.Ordinal)
        && _placement.Find(tab.Key[KeyPrefix.Length..]) is not { Location: ToolWindowLocation.Editor };
}
