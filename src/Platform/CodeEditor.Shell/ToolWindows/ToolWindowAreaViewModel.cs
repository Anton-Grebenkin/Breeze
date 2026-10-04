using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ToolWindows;

/// <summary>
/// A layout area (side bar or bottom panel): its tool windows, the active one, visibility and size. Which tool windows
/// belong to the area is decided by <see cref="ToolWindowPlacement"/>, since users move them between areas (ADR 0031).
/// </summary>
public sealed partial class ToolWindowAreaViewModel : ObservableObject, IDisposable
{
    private readonly ToolWindowPlacement _placement;
    private readonly double _minSize;
    private string? _pendingActiveId;

    public ToolWindowAreaViewModel(
        ToolWindowLocation location,
        double defaultSize,
        double minSize,
        ToolWindowPlacement placement)
    {
        Location = location;
        _minSize = minSize;
        _placement = placement;
        Size = defaultSize;

        _placement.Changed += OnPlacementChanged;
        Rebuild();
    }

    public ToolWindowLocation Location { get; }

    [ObservableProperty]
    public partial IReadOnlyList<ToolWindowViewModel> Items { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial ToolWindowViewModel? Active { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridSize))]
    public partial bool IsVisible { get; private set; }

    /// <summary>The bottom panel fills the editor area height, like "Maximize Panel" in VS Code (ADR 0031).</summary>
    [ObservableProperty]
    public partial bool IsMaximized { get; private set; }

    /// <summary>Only the bottom panel can be maximized; side bars are already full height.</summary>
    public bool CanMaximize => Location == ToolWindowLocation.Panel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridSize))]
    public partial double Size { get; private set; }

    public string Title => Active?.Title ?? string.Empty;

    /// <summary>Size for the layout grid: 0 while hidden. The splitter changes it, clamped to the minimum.</summary>
    public double GridSize
    {
        get => IsVisible ? Size : 0;
        set
        {
            if (IsVisible)
            {
                Size = Math.Max(value, _minSize);
            }
        }
    }

    public static string ShowCommandId(string toolWindowId) => $"workbench.view.{toolWindowId}";

    public bool Show(string id)
    {
        var item = Find(id);
        if (item is null)
        {
            return false;
        }

        SetActive(item);
        IsVisible = true;
        return true;
    }

    /// <summary>Icon or tab click; clicking the active visible tool window again hides the area (as in VS Code).</summary>
    [RelayCommand]
    public void ToggleItem(ToolWindowViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (IsVisible && ReferenceEquals(item, Active))
        {
            IsVisible = false;
            return;
        }

        Show(item.Id);
    }

    /// <summary>Maximizes or restores; a hidden panel is shown first.</summary>
    [RelayCommand]
    public void ToggleMaximized()
    {
        if (!CanMaximize)
        {
            return;
        }

        if (!IsVisible)
        {
            Toggle();
        }

        IsMaximized = IsVisible && !IsMaximized;
    }

    [RelayCommand]
    public void Toggle()
    {
        if (IsVisible)
        {
            IsVisible = false;
            return;
        }

        var target = Active ?? (Items.Count > 0 ? Items[0] : null);
        if (target is not null)
        {
            Show(target.Id);
        }
    }

    [RelayCommand]
    public void Hide() => IsVisible = false;

    partial void OnIsVisibleChanged(bool value)
    {
        if (!value)
        {
            IsMaximized = false;
        }
    }

    public ToolWindowAreaState Capture() => new(IsVisible, Size, Active?.Id);

    public void Restore(ToolWindowAreaState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Size = Math.Max(state.Size, _minSize);
        _pendingActiveId = state.ActiveId;
        if (state.ActiveId is not null && Show(state.ActiveId))
        {
            _pendingActiveId = null;
        }

        IsVisible = state.IsVisible && Active is not null;
    }

    public void Dispose() => _placement.Changed -= OnPlacementChanged;

    private void OnPlacementChanged(object? sender, EventArgs e) => Rebuild();

    // Tool window models live in the placement, so created content survives registry changes and moves.
    private void Rebuild()
    {
        Items = _placement.In(Location);

        if (Active is not null && Find(Active.Id) is null)
        {
            SetActive(null);
            IsVisible = false;
        }

        if (_pendingActiveId is not null && Find(_pendingActiveId) is { } pending)
        {
            SetActive(pending);
            _pendingActiveId = null;
        }
    }

    private ToolWindowViewModel? Find(string id) => Items.FirstOrDefault(item => item.Id == id);

    private void SetActive(ToolWindowViewModel? item)
    {
        if (Active is not null)
        {
            Active.IsActive = false;
        }

        Active = item;
        if (item is not null)
        {
            item.IsActive = true;
        }
    }
}
