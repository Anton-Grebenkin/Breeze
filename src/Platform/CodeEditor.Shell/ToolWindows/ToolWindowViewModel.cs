using CodeEditor.Shell.Menus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ToolWindows;

/// <summary>
/// A module tool window. Content is created on first access, so modules activate lazily. One instance lives for the
/// app's lifetime (<see cref="ToolWindowPlacement"/>), so moving it doesn't recreate content. <see cref="MoveMenu"/>
/// backs the "Move to…" menu on its tab and icon (ADR 0031).
/// </summary>
/// <param name="move">Requests a move to an area; the layout performs it.</param>
public sealed partial class ToolWindowViewModel(ToolWindowDefinition definition, Func<string?> shortcut, Action<string, ToolWindowLocation>? move = null)
    : ObservableObject
{
    private object? _content;

    public ToolWindowDefinition Definition { get; } = definition;

    public string Id => Definition.Id;

    public string Title => Definition.Title;

    public string Icon => Definition.Icon;

    /// <summary>Icon tooltip, e.g. "Output (Ctrl+Shift+U)". The keybinding is read on hover, so it is always current.</summary>
    public string ToolTip => shortcut() is { } keys ? $"{Definition.Title} ({keys})" : Definition.Title;

    public string AutomationId => $"ToolWindow.{Definition.Id}";

    public object Content => _content ??= Definition.CreateContent();

    public bool IsContentCreated => _content is not null;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Current area; by default where the module declared it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveMenu))]
    public partial ToolWindowLocation Location { get; set; } = definition.Location;

    /// <summary>"Move to…" items for every area except the current one.</summary>
    public IReadOnlyList<MenuItemViewModel> MoveMenu =>
        [.. ToolWindowLocations.All.Where(target => target != Location).Select(MoveItem)];

    private MenuItemViewModel MoveItem(ToolWindowLocation target) =>
        MenuItemViewModel.ForAction(ToolWindowLocations.MoveTitle(target), $"ToolWindow.{Id}.MoveTo.{target}", new AsyncRelayCommand(() =>
        {
            move?.Invoke(Id, target);
            return Task.CompletedTask;
        }));
}
