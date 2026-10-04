using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Viewers.Commands;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using static CodeEditor.Modules.Viewers.Tests.Infrastructure.ViewersFixture;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Viewer commands on a real tab area: context keys follow the active tab, zoom and go to offset act on their own tab,
/// SVG reopens as text in place of the picture, a binary file shows bytes instead of an error; zoom keys and Ctrl+G
/// belong to the viewer in its tab and to the interface zoom and the text elsewhere.
/// </summary>
public sealed class ViewerCommandsTests : IDisposable
{
    private const string InterfaceZoomId = "workbench.action.zoomIn";

    private readonly ViewersFixture _fixture = new();
    private readonly ContextKeyService _context = new();
    private readonly CommandRegistry _registry = new();
    private readonly KeybindingRegistry _keybindings = new();
    private readonly MenuRegistry _menus = new();
    private readonly FakeQuickPick _quickPick = new();
    private readonly DocumentService _documents;
    private readonly EditorAreaViewModel _area;
    private readonly ViewerContextKeys _keys;
    private readonly ViewerCommands _commands;

    public ViewerCommandsTests()
    {
        _fixture.Files.AddBytes(PathOf("pic.png"), [0x89, 0x50]).AddFile(PathOf("logo.svg"), "<svg/>").AddFile(PathOf("Program.cs"), "class P {}");
        _fixture.Bytes.AddGenerated(PathOf("data.bin"), 4096);
        _documents = new DocumentService(_fixture.Files, new TestTextBufferFactory(), new InlineUiDispatcher(), _fixture.Workspace, NullLogger<DocumentService>.Instance);
        var statusBar = new StatusBarViewModel();
        _area = new EditorAreaViewModel(_documents, [new PlainEditorProvider()], [new ViewerProvider(_fixture.Context)], new DocumentSaver(_documents, new FakeDialogs(), statusBar), _context, statusBar);
        _keys = new ViewerContextKeys(_area, _context);
        _keys.Start();

        // Interface zoom is registered before the module, as in the app.
        _keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+="), InterfaceZoomId));
        _commands = new ViewerCommands(_area, _quickPick);
        _commands.Register(_registry, _keybindings, _menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _keys.Dispose();
        _documents.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task ContextKeys_FollowTheActiveTab()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("pic.png")));
        var onImage = Keys();
        await _area.OpenAsync(new OpenFileRequest(PathOf("logo.svg")));
        var onSvg = Keys();
        await _area.OpenAsync(new OpenFileRequest(PathOf("data.bin")));
        var onBytes = Keys();
        await _area.OpenAsync(new OpenFileRequest(PathOf("Program.cs")));

        Assert.Equal((true, true, false, false), onImage);
        Assert.Equal((true, true, true, false), onSvg);
        Assert.Equal((true, false, false, true), onBytes);
        Assert.Equal((false, false, false, false), Keys());
    }

    [Fact]
    public async Task ZoomCommands_ActOnTheActiveImage()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("pic.png")));
        var image = (ImageViewerViewModel)_area.Active!.Editor;

        await RunAsync(ViewerCommands.ZoomInId);
        var zoomedIn = (image.Zoom.Scale, image.Zoom.IsFit);
        await RunAsync(ViewerCommands.ZoomToFitId);

        Assert.Equal((1.5, false), zoomedIn);
        Assert.True(image.Zoom.IsFit);
    }

    [Fact]
    public async Task OpenAsText_ReplacesTheSvgViewerInPlace()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("logo.svg")));
        Assert.IsType<SvgViewerViewModel>(_area.Active!.Editor);

        await RunAsync(ViewerCommands.OpenAsTextId);

        var tab = Assert.IsType<EditorTabViewModel>(Assert.Single(_area.Tabs));
        Assert.Equal("<svg/>", tab.Document.Buffer.GetText());
    }

    [Fact]
    public async Task BinaryFile_OpensAsBytes_InsteadOfAnError()
    {
        _fixture.Files.AddBytes(PathOf("blob.xyz"), [0x00, 0x01, 0x02, 0x00]);

        var tab = await _area.OpenAsync(new OpenFileRequest(PathOf("blob.xyz")));

        Assert.IsType<HexViewerViewModel>(Assert.IsType<ViewTabViewModel>(tab).Editor);
    }

    [Fact]
    public async Task GoToOffset_AsksForTheOffsetInThePalette()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("data.bin")));

        await RunAsync(ViewerCommands.GoToOffsetId);

        Assert.IsType<OffsetQuickOpenProvider>(_quickPick.Shown);
    }

    [Fact]
    public async Task Keys_BelongToTheViewer_OnlyInItsTab()
    {
        await _area.OpenAsync(new OpenFileRequest(PathOf("pic.png")));
        var onImage = (Resolve("Ctrl+="), Resolve("Ctrl+Subtract"), Resolve("Ctrl+0"));
        await _area.OpenAsync(new OpenFileRequest(PathOf("data.bin")));
        var onBytes = (Resolve("Ctrl+="), Resolve("Ctrl+G"));
        await _area.OpenAsync(new OpenFileRequest(PathOf("Program.cs")));

        Assert.Equal((ViewerCommands.ZoomInId, ViewerCommands.ZoomOutId, ViewerCommands.ZoomActualSizeId), onImage);
        Assert.Equal((InterfaceZoomId, ViewerCommands.GoToOffsetId), onBytes);
        Assert.Equal((InterfaceZoomId, (string?)null), (Resolve("Ctrl+="), Resolve("Ctrl+G")));
    }

    [Fact]
    public void TabContextMenu_HasTheViewerCommands() =>
        Assert.Equal(
            [
                ViewerCommands.ZoomInId, ViewerCommands.ZoomOutId, ViewerCommands.ZoomActualSizeId, ViewerCommands.ZoomToFitId,
                ViewerCommands.GoToOffsetId, ViewerCommands.OpenAsTextId, ViewerCommands.RefreshId, ViewerCommands.OpenExternalId,
            ],
            _menus.GetItems(EditorCommands.TabContextMenuId).Select(item => item.CommandId));

    [Fact]
    public void Commands_AreInThePalette_UnderTheirCategory()
    {
        Assert.True(_registry.TryGet(ViewerCommands.ZoomActualSizeId, out var command));
        Assert.Equal("Просмотр файла: Реальный размер (100 %)", command.DisplayTitle);
    }

    private (bool Viewer, bool Zoomable, bool Text, bool Hex) Keys() =>
        (Is(ViewerContextKeys.ViewerActiveKey), Is(ViewerContextKeys.ZoomableActiveKey), Is(ViewerContextKeys.TextActiveKey), Is(ViewerContextKeys.HexActiveKey));

    private bool Is(string key) => _context.Evaluate(ContextExpression.Parse(key));

    private string? Resolve(string keys) =>
        new KeybindingResolver(_keybindings).Resolve(KeySequence.Parse(keys).First, _context).Binding?.CommandId;

    private async Task RunAsync(string id)
    {
        Assert.True(_registry.TryGet(id, out var command));
        await command.Handler(null, TestContext.Current.CancellationToken);
    }

    private sealed class PlainEditorProvider : IEditorProvider
    {
        public int Priority => 0;

        public bool CanOpen(string filePath) => true;

        public object CreateEditor(IDocument document) => document;
    }
}
