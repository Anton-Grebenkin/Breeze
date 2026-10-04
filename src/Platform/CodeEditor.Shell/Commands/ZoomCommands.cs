using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Zoom;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Interface zoom commands with VS Code ids and keys (<see cref="ZoomKeys"/>): <c>Ctrl+=</c>, <c>Ctrl+-</c>,
/// <c>Ctrl+0</c>, and View menu items. Ctrl + wheel outside the editor does the same (the window view).
/// </summary>
public sealed class ZoomCommands(WindowZoom zoom) : IDisposable
{
    public const string ZoomInId = "workbench.action.zoomIn";
    public const string ZoomOutId = "workbench.action.zoomOut";
    public const string ZoomResetId = "workbench.action.zoomReset";

    private const string MenuGroup = "5_zoom";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        (string Id, string Title, string MenuTitle, Action Action, IEnumerable<string> Keys)[] items =
        [
            (ZoomInId, Strings.ZoomIn, Strings.ZoomInMenu, zoom.ZoomIn, ZoomKeys.In),
            (ZoomOutId, Strings.ZoomOut, Strings.ZoomOutMenu, zoom.ZoomOut, ZoomKeys.Out),
            (ZoomResetId, Strings.ZoomReset, Strings.ZoomResetMenu, zoom.Reset, ZoomKeys.Reset),
        ];

        for (var order = 0; order < items.Length; order++)
        {
            var (id, title, menuTitle, action, keys) = items[order];
            _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
            {
                action();
                return ValueTask.CompletedTask;
            }, Strings.CategoryView)));
            _registrations.AddRange(keys.Select(gesture => keybindings.Register(new KeybindingDefinition(KeySequence.Parse(gesture), id))));
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.View, id, MenuGroup, order, menuTitle)));
        }
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }
}
