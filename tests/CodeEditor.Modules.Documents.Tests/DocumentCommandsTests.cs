using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Documents.Commands;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using static CodeEditor.Modules.Documents.Tests.DocumentsFixture;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>
/// Viewer commands on a real editor area: documents open in a viewer, context keys follow the active tab, CSV opens as
/// text instead of a grid, items appear in the tab context menu.
/// </summary>
public sealed class DocumentCommandsTests : IDisposable
{
    private readonly DocumentsFixture _fixture = new();
    private readonly ContextKeyService _context = new();
    private readonly CommandRegistry _registry = new();
    private readonly MenuRegistry _menus = new();
    private readonly DocumentService _documents;
    private readonly EditorAreaViewModel _area;
    private readonly DocumentCommands _commands;

    public DocumentCommandsTests()
    {
        _fixture.Files.AddFile(PathOf("data.csv"), "a;b\n1;2\n").AddFile(PathOf("Program.cs"), "class P {}");
        _documents = new DocumentService(_fixture.Files, new TestTextBufferFactory(), new InlineUiDispatcher(), _fixture.Workspace, NullLogger<DocumentService>.Instance);
        var viewers = new DocumentViewerProvider(new DocumentViewerContext(_fixture.Files, _fixture.Workspace, new FakeSystemShell(), new InlineUiDispatcher(), TimeProvider.System, _fixture.Commands));
        var statusBar = new StatusBarViewModel();
        _area = new EditorAreaViewModel(_documents, [new PlainEditorProvider()], [viewers], new DocumentSaver(_documents, new FakeDialogs(), statusBar), _context, statusBar);
        _commands = new DocumentCommands(_area, _context);
        _commands.Register(_registry, _menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _documents.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task ContextKeys_FollowTheActiveTab()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("data.csv")));
        var onCsv = (_context.Evaluate(ContextExpression.Parse(DocumentCommands.ViewerActiveContextKey)), _context.Evaluate(ContextExpression.Parse(DocumentCommands.CsvActiveContextKey)));

        await _area.OpenAsync(new OpenFileRequest(PathOf("Program.cs")));

        Assert.Equal((true, true), onCsv);
        Assert.False(_context.Evaluate(ContextExpression.Parse(DocumentCommands.ViewerActiveContextKey)));
    }

    // The CSV grid is read-only: the command replaces the viewer with a text tab for the same file.
    [Fact]
    public async Task OpenAsText_ReplacesTheViewerWithATextTab()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("data.csv")));
        Assert.IsType<SpreadsheetViewerViewModel>(_area.Active!.Editor);

        Assert.True(_registry.TryGet(DocumentCommands.OpenAsTextId, out var command));
        await command.Handler(null, TestContext.Current.CancellationToken);

        var tab = Assert.Single(_area.Tabs);
        Assert.IsType<EditorTabViewModel>(tab);
        Assert.Equal("a;b\n1;2\n", ((EditorTabViewModel)tab).Document.Buffer.GetText());
    }

    [Fact]
    public void TabContextMenu_HasTheDocumentCommands() =>
        Assert.Equal(
            [DocumentCommands.RefreshId, DocumentCommands.OpenExternalId, DocumentCommands.OpenAsTextId],
            _menus.GetItems(EditorCommands.TabContextMenuId).Select(item => item.CommandId));

    private sealed class PlainEditorProvider : IEditorProvider
    {
        public int Priority => 0;

        public bool CanOpen(string filePath) => true;

        public object CreateEditor(IDocument document) => document;
    }
}
