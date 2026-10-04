using CodeEditor.Core.Keybindings;

namespace CodeEditor.Shell.ToolWindows;

/// <summary>
/// Where each tool window is (ADR 0031). By default where the module declared it; the user can move it to either side
/// bar, the bottom panel or the editor area (as a tab next to files). Each tool window has one model for the app's
/// lifetime, so moving doesn't recreate content. Moves are saved in the layout. UI thread only.
/// </summary>
public sealed class ToolWindowPlacement : IDisposable
{
    private readonly IToolWindowRegistry _registry;
    private readonly IKeybindingRegistry _keybindings;
    private readonly Dictionary<string, ToolWindowLocation> _moved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToolWindowViewModel> _items = new(StringComparer.Ordinal);

    public ToolWindowPlacement(IToolWindowRegistry registry, IKeybindingRegistry keybindings)
    {
        _registry = registry;
        _keybindings = keybindings;
        _registry.Changed += OnRegistryChanged;
    }

    /// <summary>Raised when the set of tool windows or their locations change.</summary>
    public event EventHandler? Changed;

    /// <summary>The user chose "Move to…" on a tool window tab or icon; the layout performs the move.</summary>
    public event EventHandler<ToolWindowMoveRequest>? MoveRequested;

    /// <summary>The area's tool windows in order.</summary>
    public IReadOnlyList<ToolWindowViewModel> In(ToolWindowLocation location) =>
        [.. Definitions().Where(definition => LocationOf(definition) == location).OrderBy(definition => definition.Order).Select(ViewModelOf)];

    /// <summary>All tool windows, for the palette picker.</summary>
    public IReadOnlyList<ToolWindowViewModel> All => [.. Definitions().OrderBy(definition => definition.Title, StringComparer.CurrentCulture).Select(ViewModelOf)];

    public ToolWindowViewModel? Find(string id) =>
        Definitions().FirstOrDefault(definition => definition.Id == id) is { } definition ? ViewModelOf(definition) : null;

    public ToolWindowLocation LocationOf(ToolWindowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _moved.GetValueOrDefault(definition.Id, definition.Location);
    }

    /// <returns><c>false</c> if there is no such tool window or it is already there.</returns>
    public bool Move(string id, ToolWindowLocation location)
    {
        if (Find(id) is not { } toolWindow || toolWindow.Location == location)
        {
            return false;
        }

        if (location == toolWindow.Definition.Location)
        {
            _moved.Remove(id);
        }
        else
        {
            _moved[id] = location;
        }

        Update();
        return true;
    }

    /// <summary>Moves all tool windows back to where modules declared them.</summary>
    public void Reset()
    {
        if (_moved.Count > 0)
        {
            _moved.Clear();
            Update();
        }
    }

    /// <summary>Moves for the layout: tool window id → area name.</summary>
    public Dictionary<string, string> Capture() => _moved.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

    /// <summary>
    /// Restores moves from the layout; unknown areas are skipped, and moves of tool windows not yet declared wait for them.
    /// </summary>
    public void Restore(IReadOnlyDictionary<string, string>? moved)
    {
        _moved.Clear();
        foreach (var (id, name) in moved ?? new Dictionary<string, string>())
        {
            if (Enum.TryParse<ToolWindowLocation>(name, out var location))
            {
                _moved[id] = location;
            }
        }

        Update();
    }

    public void Dispose() => _registry.Changed -= OnRegistryChanged;

    private void OnRegistryChanged(object? sender, EventArgs e) => Update();

    private IEnumerable<ToolWindowDefinition> Definitions() => Enum.GetValues<ToolWindowLocation>().SelectMany(_registry.GetAll);

    private ToolWindowViewModel ViewModelOf(ToolWindowDefinition definition)
    {
        if (!_items.TryGetValue(definition.Id, out var item) || !ReferenceEquals(item.Definition, definition))
        {
            item = new ToolWindowViewModel(definition, () => ShortcutOf(definition.Id), RequestMove) { Location = LocationOf(definition) };
            _items[definition.Id] = item;
        }

        return item;
    }

    private void Update()
    {
        foreach (var definition in Definitions())
        {
            ViewModelOf(definition).Location = LocationOf(definition);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RequestMove(string id, ToolWindowLocation location) => MoveRequested?.Invoke(this, new ToolWindowMoveRequest(id, location));

    private string? ShortcutOf(string toolWindowId) =>
        _keybindings.FindForCommand(ToolWindowAreaViewModel.ShowCommandId(toolWindowId))?.Sequence.ToString();
}
