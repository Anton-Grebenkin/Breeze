using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Zoom;

namespace CodeEditor.Modules.Viewers.Commands;

/// <summary>
/// Viewer commands (ADR 0037): refresh, open in an external app, open SVG as text, image zoom (<c>Ctrl+=</c>,
/// <c>Ctrl+-</c>, <c>Ctrl+0</c> for 100 % with their keypad variants, fit to window without a key), go to offset
/// (<c>Ctrl+G</c> in the hex view). The keys apply only while a viewer tab is active; otherwise they are interface zoom
/// and go to line. All commands are in the palette and the tab context menu; the viewer toolbar has buttons for the
/// same actions.
/// </summary>
public sealed class ViewerCommands(EditorAreaViewModel editors, IQuickPick quickPick) : IDisposable
{
    public const string RefreshId = "viewers.refresh";
    public const string OpenExternalId = "viewers.openExternal";
    public const string OpenAsTextId = "viewers.openAsText";
    public const string ZoomInId = "viewers.zoomIn";
    public const string ZoomOutId = "viewers.zoomOut";
    public const string ZoomActualSizeId = "viewers.zoomActualSize";
    public const string ZoomToFitId = "viewers.zoomToFit";
    public const string GoToOffsetId = "viewers.goToOffset";

    private const string ZoomMenuGroup = "3_viewerZoom";
    private const string MenuGroup = "4_viewer";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(keybindings);
        ArgumentNullException.ThrowIfNull(menus);
        var viewer = ContextExpression.Parse(ViewerContextKeys.ViewerActiveKey);
        var zoomable = ContextExpression.Parse(ViewerContextKeys.ZoomableActiveKey);
        var text = ContextExpression.Parse(ViewerContextKeys.TextActiveKey);
        var hex = ContextExpression.Parse(ViewerContextKeys.HexActiveKey);

        Add(commands, RefreshId, Strings.CommandRefresh, _ => Active<ViewerViewModel>()?.RefreshAsync() ?? Task.CompletedTask, viewer);
        Add(commands, OpenExternalId, Strings.CommandOpenExternal, _ => Run<ViewerViewModel>(view => view.OpenExternal()), viewer);
        Add(commands, OpenAsTextId, Strings.CommandOpenAsText, argument => OpenAsTextAsync(argument as string ?? Active<ViewerViewModel>()?.FilePath), text);
        Add(commands, ZoomInId, Strings.CommandZoomIn, _ => Run<IZoomableViewer>(view => view.Zoom.ZoomIn()), zoomable);
        Add(commands, ZoomOutId, Strings.CommandZoomOut, _ => Run<IZoomableViewer>(view => view.Zoom.ZoomOut()), zoomable);
        Add(commands, ZoomActualSizeId, Strings.CommandZoomActualSize, _ => Run<IZoomableViewer>(view => view.Zoom.ActualSize()), zoomable);
        Add(commands, ZoomToFitId, Strings.CommandZoomToFit, _ => Run<IZoomableViewer>(view => view.Zoom.Fit()), zoomable);
        Add(commands, GoToOffsetId, Strings.CommandGoToOffset, _ => Run<HexViewerViewModel>(view => quickPick.Show(new OffsetQuickOpenProvider(view))), hex);

        // Interface zoom (Ctrl+=) and go to line (Ctrl+G) are registered earlier; in a viewer tab these keys belong to it.
        BindAll(keybindings, ZoomKeys.In, ZoomInId, zoomable);
        BindAll(keybindings, ZoomKeys.Out, ZoomOutId, zoomable);
        BindAll(keybindings, ZoomKeys.Reset, ZoomActualSizeId, zoomable);
        Bind(keybindings, "Ctrl+G", GoToOffsetId, hex);

        RegisterTabMenu(menus, zoomable, text, hex, viewer);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void RegisterTabMenu(IMenuRegistry menus, ContextExpression zoomable, ContextExpression text, ContextExpression hex, ContextExpression viewer)
    {
        (string Id, string Group, string Title, ContextExpression When)[] items =
        [
            (ZoomInId, ZoomMenuGroup, Strings.MenuZoomIn, zoomable),
            (ZoomOutId, ZoomMenuGroup, Strings.MenuZoomOut, zoomable),
            (ZoomActualSizeId, ZoomMenuGroup, Strings.MenuZoomActualSize, zoomable),
            (ZoomToFitId, ZoomMenuGroup, Strings.MenuZoomToFit, zoomable),
            (GoToOffsetId, MenuGroup, Strings.MenuGoToOffset, hex),
            (OpenAsTextId, MenuGroup, Strings.MenuOpenAsText, text),
            (RefreshId, MenuGroup, Strings.MenuRefresh, viewer),
            (OpenExternalId, MenuGroup, Strings.MenuOpenExternal, viewer),
        ];
        for (var index = 0; index < items.Length; index++)
        {
            var (id, group, title, when) = items[index];
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(EditorCommands.TabContextMenuId, id, group, index, title, when)));
        }
    }

    // Replaces the picture tab with a text tab in place: SVG is a text format (IsText).
    private async Task OpenAsTextAsync(string? path)
    {
        if (path is not null)
        {
            await editors.OpenTextAsync(new OpenFileRequest(path));
        }
    }

    private T? Active<T>()
        where T : class => editors.Active?.Editor as T;

    private Task Run<T>(Action<T> action)
        where T : class
    {
        if (Active<T>() is { } view)
        {
            action(view);
        }

        return Task.CompletedTask;
    }

    private void Add(ICommandRegistry commands, string id, string title, Func<object?, Task> handler, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (argument, _) => await handler(argument), Strings.Category, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, ContextExpression when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, when)));

    private void BindAll(IKeybindingRegistry keybindings, IEnumerable<string> gestures, string commandId, ContextExpression when)
    {
        foreach (var keys in gestures)
        {
            Bind(keybindings, keys, commandId, when);
        }
    }
}
