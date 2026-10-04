using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Layout commands: toggle the side and bottom areas (Ctrl+B, Ctrl+J as in VS Code), a "show" command with keybinding
/// and View menu item for every module tool window, and moving tool windows between areas and into the editor area
/// (ADR 0031).
/// </summary>
public sealed class LayoutCommands(WorkbenchLayout layout, EditorToolWindows editorWindows, IQuickPick quickPick) : IDisposable
{
    public const string ToggleSideBarId = "workbench.sideBar.toggle";
    public const string ToggleSecondarySideBarId = "workbench.secondarySideBar.toggle";
    public const string TogglePanelId = "workbench.panel.toggle";
    public const string ToggleMaximizedPanelId = "workbench.panel.toggleMaximized";
    public const string MoveViewId = "workbench.action.moveView";
    public const string ResetViewLocationsId = "workbench.action.resetViewLocations";

    /// <summary>Moves the tool window in the active editor tab to an area; the id is this prefix + area name.</summary>
    public const string MoveEditorViewPrefix = "workbench.action.moveEditorView.";

    private const string LayoutGroup = "3_layout";
    private const string ToolWindowsGroup = "4_toolwindows";
    private const string MoveGroup = "9_move";

    private readonly List<IDisposable> _registrations = [];
    private readonly Dictionary<string, List<IDisposable>> _toolWindowRegistrations = new(StringComparer.Ordinal);

    private ICommandRegistry? _commands;
    private IKeybindingRegistry? _keybindings;
    private IMenuRegistry? _menus;
    private IToolWindowRegistry? _toolWindows;

    public void Register(
        ICommandRegistry commands,
        IKeybindingRegistry keybindings,
        IMenuRegistry menus,
        IToolWindowRegistry toolWindows)
    {
        (_commands, _keybindings, _menus, _toolWindows) = (commands, keybindings, menus, toolWindows);

        RegisterToggle(ToggleSideBarId, Strings.SideBar, "Ctrl+B", layout.SideBar, order: 1);
        RegisterToggle(TogglePanelId, Strings.Panel, "Ctrl+J", layout.Panel, order: 2);
        RegisterToggle(ToggleSecondarySideBarId, Strings.SecondarySideBar, "Ctrl+Alt+B", layout.SecondarySideBar, order: 3);
        Add(ToggleMaximizedPanelId, Strings.ToggleMaximizedPanel, layout.Panel.ToggleMaximized);
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.View, ToggleMaximizedPanelId, LayoutGroup, order: 6)));
        RegisterMoveCommands();

        toolWindows.Changed += OnToolWindowsChanged;
        SyncToolWindowCommands();
    }

    public void Dispose()
    {
        if (_toolWindows is not null)
        {
            _toolWindows.Changed -= OnToolWindowsChanged;
        }

        foreach (var registration in _registrations.Concat(_toolWindowRegistrations.Values.SelectMany(list => list)))
        {
            registration.Dispose();
        }

        _registrations.Clear();
        _toolWindowRegistrations.Clear();
    }

    private void RegisterToggle(string id, string title, string keys, ToolWindowAreaViewModel area, int order)
    {
        Add(id, Format(Strings.ToggleVisibility, title), area.Toggle);
        _registrations.Add(_keybindings!.Register(new KeybindingDefinition(KeySequence.Parse(keys), id)));
        _registrations.Add(_menus!.Register(MenuItemDefinition.ForCommand(MenuIds.View, id, LayoutGroup, order, title)));
    }

    // Palette and View menu pick a tool window and an area; the editor tab menu moves a tool window back to an area.
    private void RegisterMoveCommands()
    {
        Add(MoveViewId, Strings.MoveView, PickView);
        Add(ResetViewLocationsId, Strings.ResetViewLocations, layout.Placement.Reset);
        _registrations.Add(_menus!.Register(MenuItemDefinition.ForCommand(MenuIds.View, MoveViewId, LayoutGroup, order: 4)));
        _registrations.Add(_menus.Register(MenuItemDefinition.ForCommand(MenuIds.View, ResetViewLocationsId, LayoutGroup, order: 5)));

        var isToolWindow = ContextExpression.Parse(EditorToolWindows.ActiveIsToolWindowContextKey);
        var order = 0;
        foreach (var location in ToolWindowLocations.All.Where(location => location != ToolWindowLocation.Editor))
        {
            var id = MoveEditorViewPrefix + location;
            Add(id, ToolWindowLocations.MoveTitle(location), () => MoveActiveEditorView(location), isToolWindow);
            _registrations.Add(_menus.Register(MenuItemDefinition.ForCommand(EditorCommands.TabContextMenuId, id, MoveGroup, order++, when: isToolWindow)));
        }
    }

    private void MoveActiveEditorView(ToolWindowLocation location)
    {
        if (editorWindows.Active is { } toolWindow)
        {
            layout.Move(toolWindow.Id, location);
        }
    }

    private void PickView()
    {
        var items = layout.Placement.All
            .Select(toolWindow => new QuickPickItem(toolWindow.Id, toolWindow.Title) { Detail = ToolWindowLocations.Title(toolWindow.Location) })
            .ToList();
        quickPick.Show(new QuickPickProvider(Strings.PickViewToMove, items, item =>
        {
            PickLocation(item.Id);
            return Task.CompletedTask;
        }));
    }

    private void PickLocation(string id)
    {
        if (layout.Placement.Find(id) is not { } toolWindow)
        {
            return;
        }

        var targets = ToolWindowLocations.All
            .Where(location => location != toolWindow.Location)
            .Select(location => new QuickPickItem(location.ToString(), ToolWindowLocations.Title(location)))
            .ToList();
        quickPick.Show(new QuickPickProvider(Format(Strings.PickViewLocation, toolWindow.Title), targets, item =>
        {
            layout.Move(id, Enum.Parse<ToolWindowLocation>(item.Id));
            return Task.CompletedTask;
        }));
    }

    private void OnToolWindowsChanged(object? sender, EventArgs e) => SyncToolWindowCommands();

    /// <summary>Syncs tool window commands with the registry: registers new ones and removes stale ones.</summary>
    private void SyncToolWindowCommands()
    {
        var current = Enum.GetValues<ToolWindowLocation>()
            .SelectMany(location => _toolWindows!.GetAll(location))
            .ToDictionary(toolWindow => toolWindow.Id, StringComparer.Ordinal);

        foreach (var removed in _toolWindowRegistrations.Keys.Where(id => !current.ContainsKey(id)).ToArray())
        {
            _toolWindowRegistrations[removed].ForEach(registration => registration.Dispose());
            _toolWindowRegistrations.Remove(removed);
        }

        foreach (var toolWindow in current.Values.Where(toolWindow => !_toolWindowRegistrations.ContainsKey(toolWindow.Id)))
        {
            _toolWindowRegistrations[toolWindow.Id] = RegisterShowCommand(toolWindow);
        }
    }

    // Shows the tool window where it currently is: the user may have moved it to another area or the editor.
    private List<IDisposable> RegisterShowCommand(ToolWindowDefinition toolWindow)
    {
        var commandId = ToolWindowAreaViewModel.ShowCommandId(toolWindow.Id);
        var registrations = new List<IDisposable>
        {
            _commands!.Register(new CommandDefinition(commandId, Format(Strings.ShowToolWindow, toolWindow.Title), (_, _) =>
            {
                layout.Show(toolWindow.Id);
                return ValueTask.CompletedTask;
            }, Strings.CategoryView)),
            _menus!.Register(MenuItemDefinition.ForCommand(MenuIds.View, commandId, ToolWindowsGroup, toolWindow.Order, toolWindow.Title)),
        };

        if (toolWindow.Keybinding is not null)
        {
            registrations.Add(_keybindings!.Register(new KeybindingDefinition(KeySequence.Parse(toolWindow.Keybinding), commandId)));
        }

        return registrations;
    }

    private void Add(string id, string title, Action action, ContextExpression? when = null) =>
        _registrations.Add(_commands!.Register(new CommandDefinition(id, title, (_, _) =>
        {
            action();
            return ValueTask.CompletedTask;
        }, Strings.CategoryView, when)));

    private static string Format(string template, string argument) =>
        string.Format(CultureInfo.CurrentCulture, template, argument);
}
