using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Zoom;

namespace CodeEditor.Modules.Diagrams.Commands;

/// <summary>
/// Diagram commands (ADR 0035): preview to the side (<c>Ctrl+K V</c>, like the VS Code Markdown preview), SVG and
/// PNG export, preview zoom (<c>Ctrl+=</c>, <c>Ctrl+-</c>, <c>Ctrl+0</c> and their keypad variants while the preview tab
/// is active).
/// Items live in the tab context menu and the View menu; all commands are in the palette.
/// </summary>
public sealed class DiagramCommands(EditorAreaViewModel editors, DiagramPreviews previews, DiagramExports exports) : IDisposable
{
    public const string OpenPreviewToSideId = "diagram.openPreviewToSide";
    public const string ExportSvgId = "diagram.exportSvg";
    public const string ExportPngId = "diagram.exportPng";
    public const string ZoomInId = "diagram.zoomIn";
    public const string ZoomOutId = "diagram.zoomOut";
    public const string ZoomResetId = "diagram.zoomReset";
    public const string ZoomToFitId = "diagram.zoomToFit";

    private const string MenuGroup = "3_diagram";
    private const string ViewMenuGroup = "6_diagram";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var diagram = ContextExpression.Parse(DiagramEditorContext.DiagramActiveKey);
        var preview = ContextExpression.Parse(DiagramEditorContext.PreviewActiveKey);
        var diagramOrPreview = ContextExpression.Parse($"{DiagramEditorContext.DiagramActiveKey} || {DiagramEditorContext.PreviewActiveKey}");

        Add(commands, OpenPreviewToSideId, Strings.OpenPreviewToSide, OpenPreview, diagram);
        Add(commands, ExportSvgId, Strings.ExportSvg, () => ExportAsync(DiagramFormat.Svg), diagramOrPreview);
        Add(commands, ExportPngId, Strings.ExportPng, () => ExportAsync(DiagramFormat.Png), diagramOrPreview);
        Add(commands, ZoomInId, Strings.ZoomIn, () => Preview(view => view.ZoomInCommand.Execute(null)), preview);
        Add(commands, ZoomOutId, Strings.ZoomOut, () => Preview(view => view.ZoomOutCommand.Execute(null)), preview);
        Add(commands, ZoomResetId, Strings.ZoomReset, () => Preview(view => view.ResetZoomCommand.Execute(null)), preview);
        Add(commands, ZoomToFitId, Strings.ZoomToFit, () => Preview(view => view.FitCommand.Execute(null)), preview);

        // Interface zoom (Ctrl+=) is registered earlier; on the preview tab these keys zoom the diagram instead.
        Bind(keybindings, "Ctrl+K V", OpenPreviewToSideId, diagram);
        BindAll(keybindings, ZoomKeys.In, ZoomInId, preview);
        BindAll(keybindings, ZoomKeys.Out, ZoomOutId, preview);
        BindAll(keybindings, ZoomKeys.Reset, ZoomResetId, preview);

        (string Id, string Title, ContextExpression When)[] tabItems =
        [
            (OpenPreviewToSideId, Strings.OpenPreviewToSideMenu, diagram),
            (ExportSvgId, Strings.ExportSvgMenu, diagramOrPreview),
            (ExportPngId, Strings.ExportPngMenu, diagramOrPreview),
        ];
        for (var i = 0; i < tabItems.Length; i++)
        {
            var (id, title, when) = tabItems[i];
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(EditorCommands.TabContextMenuId, id, MenuGroup, i, title, when)));
        }

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.View, OpenPreviewToSideId, ViewMenuGroup, 0, Strings.OpenPreviewToSideMenu, diagram)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private Task OpenPreview()
    {
        if (editors.ActiveDocument is { } document && DiagramFiles.CanContainDiagrams(document.FilePath))
        {
            previews.Open(document.FilePath);
        }

        return Task.CompletedTask;
    }

    // From a file tab: the document text with unsaved edits; from a preview tab: the preview's text.
    private async Task ExportAsync(DiagramFormat format)
    {
        if (editors.Active?.Editor is DiagramPreviewViewModel view)
        {
            await exports.ExportAsync(view.FilePath, view.TextAsync, format);
        }
        else if (editors.ActiveDocument is { } document && DiagramFiles.CanContainDiagrams(document.FilePath))
        {
            await exports.ExportAsync(document.FilePath, () => Task.FromResult(document.Buffer.GetText()), format);
        }
    }

    private Task Preview(Action<DiagramPreviewViewModel> action)
    {
        if (editors.Active?.Editor is DiagramPreviewViewModel view)
        {
            action(view);
        }

        return Task.CompletedTask;
    }

    private void Add(ICommandRegistry commands, string id, string title, Func<Task> handler, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (_, _) => await handler(), Strings.Category, when)));

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
