using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Documents.Commands;

/// <summary>
/// Document viewer commands: refresh, open in an external application, open CSV as text. Available from the palette
/// and the tab context menu while a document tab is active; the viewer also has buttons for the same actions.
/// </summary>
public sealed class DocumentCommands(EditorAreaViewModel editors, IContextKeyService context) : IDisposable
{
    /// <summary>A document viewer tab is active.</summary>
    public const string ViewerActiveContextKey = "documentViewerActive";

    /// <summary>A CSV table tab is active, so it can be opened as text.</summary>
    public const string CsvActiveContextKey = "documentViewerIsCsv";

    public const string RefreshId = "documents.refresh";
    public const string OpenExternalId = "documents.openExternal";
    public const string OpenAsTextId = "documents.openAsText";

    private const string MenuGroup = "3_document";

    private readonly List<IDisposable> _registrations = [];

    private DocumentViewerViewModel? ActiveViewer => editors.Active?.Editor as DocumentViewerViewModel;

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        var viewer = ContextExpression.Parse(ViewerActiveContextKey);
        var csv = ContextExpression.Parse(CsvActiveContextKey);
        Add(commands, RefreshId, Strings.CommandRefresh, _ => ActiveViewer?.RefreshAsync() ?? Task.CompletedTask, viewer);
        Add(commands, OpenExternalId, Strings.CommandOpenExternal, _ =>
        {
            ActiveViewer?.OpenExternal();
            return Task.CompletedTask;
        }, viewer);
        Add(commands, OpenAsTextId, Strings.CommandOpenAsText, argument => OpenAsTextAsync(argument as string ?? ActiveViewer?.FilePath), csv);

        (string Id, string Title, ContextExpression When)[] items =
        [
            (RefreshId, Strings.MenuRefresh, viewer),
            (OpenExternalId, Strings.MenuOpenExternal, viewer),
            (OpenAsTextId, Strings.MenuOpenAsText, csv),
        ];
        for (var index = 0; index < items.Length; index++)
        {
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(EditorCommands.TabContextMenuId, items[index].Id, MenuGroup, index, items[index].Title, items[index].When)));
        }

        editors.ActiveDocumentChanged += OnActiveChanged;
        UpdateContext();
    }

    public void Dispose()
    {
        editors.ActiveDocumentChanged -= OnActiveChanged;
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // Closes the viewer tab and reopens the file in the text editor so CSV can be edited by hand.
    private async Task OpenAsTextAsync(string? path)
    {
        if (path is null)
        {
            return;
        }

        if (editors.Find(path) is { Editor: DocumentViewerViewModel } tab && !await editors.CloseAsync(tab))
        {
            return;
        }

        await editors.OpenTextAsync(new OpenFileRequest(path));
    }

    private void OnActiveChanged(object? sender, EventArgs e) => UpdateContext();

    private void UpdateContext()
    {
        var active = ActiveViewer;
        context.Set(ViewerActiveContextKey, active is not null);
        context.Set(CsvActiveContextKey, active?.CanOpenAsText == true);
    }

    private void Add(ICommandRegistry commands, string id, string title, Func<object?, Task> handler, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (argument, _) => await handler(argument), Strings.CategoryDocuments, when)));
}
