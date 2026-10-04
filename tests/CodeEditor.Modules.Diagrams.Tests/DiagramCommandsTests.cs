using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Diagrams.Commands;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Diagram commands: preview to the side opens a <c>diagram.preview:&lt;path&gt;</c> tab for a file with diagrams
/// (keyboard, palette, tab context menu), export works from the file tab and the preview, zoom on the preview tab.
/// </summary>
public sealed class DiagramCommandsTests : IDisposable
{
    private readonly DiagramsFixture _fixture = new();
    private readonly CommandRegistry _registry = new();
    private readonly KeybindingRegistry _keybindings = new();
    private readonly MenuRegistry _menus = new();
    private readonly DiagramCommands _commands;
    private readonly DiagramEditorContext _context;
    private readonly CommandService _service;

    public DiagramCommandsTests()
    {
        _commands = new DiagramCommands(_fixture.Editors, new DiagramPreviews(new EditorViews(_fixture.Editors), _fixture.Services), _fixture.Exports);
        _commands.Register(_registry, _keybindings, _menus);
        _context = new DiagramEditorContext(_fixture.Editors, _fixture.Context);
        _context.Start();
        _service = new CommandService(_registry, _fixture.Context, NullLogger<CommandService>.Instance);
        _fixture.AddFile("docs/arch.mmd", "graph TD").AddFile("src/App.cs", "class App {}");
    }

    public void Dispose()
    {
        _context.Dispose();
        _commands.Dispose();
        _fixture.Dispose();
    }

    // Repeating the command from the file tab activates the open preview instead of creating a second one.
    [Fact]
    public async Task OpenPreviewToSide_OpensTheViewOnce_ToTheSide()
    {
        await _fixture.OpenAsync("docs/arch.mmd");

        await RunAsync(DiagramCommands.OpenPreviewToSideId);
        Assert.True(_service.CanExecute(DiagramCommands.ZoomInId));
        Assert.False(_service.CanExecute(DiagramCommands.OpenPreviewToSideId));
        await _fixture.OpenAsync("docs/arch.mmd");
        await RunAsync(DiagramCommands.OpenPreviewToSideId);

        var tab = Assert.IsType<ViewTabViewModel>(_fixture.Editors.Active);
        Assert.Equal("diagram.preview:" + DiagramsFixture.PathOf("docs/arch.mmd"), tab.Key);
        Assert.Equal(DiagramPreviews.IdFor(DiagramsFixture.PathOf("docs/arch.mmd")), tab.Key);
        Assert.Equal("Предпросмотр arch.mmd", tab.Title);
        Assert.IsType<DiagramPreviewViewModel>(tab.Editor);
        Assert.Equal(2, _fixture.Editors.Tabs.Count);
    }

    [Fact]
    public async Task OtherFiles_HaveNoPreview()
    {
        await _fixture.OpenAsync("src/App.cs");

        Assert.False(_service.CanExecute(DiagramCommands.OpenPreviewToSideId));
        Assert.False(_service.CanExecute(DiagramCommands.ExportSvgId));
    }

    [Fact]
    public async Task Export_FromTheFileTab_UsesUnsavedText()
    {
        var document = await _fixture.OpenAsync("docs/arch.mmd");
        document.Buffer.Replace(document.Buffer.Length, 0, "\n  A --> B");

        await RunAsync(DiagramCommands.ExportPngId);

        Assert.Equal(FakeDiagramRenderer.Png("graph TD\n  A --> B"), _fixture.FileSystem.ReadAllBytes(DiagramsFixture.PathOf("docs/arch.png")));
    }

    [Fact]
    public async Task ZoomCommands_ActOnTheActivePreview()
    {
        await _fixture.OpenAsync("docs/arch.mmd");
        await RunAsync(DiagramCommands.OpenPreviewToSideId);
        var preview = (DiagramPreviewViewModel)_fixture.Editors.Active!.Editor;
        var fits = 0;
        preview.FitRequested += (_, _) => fits++;

        await RunAsync(DiagramCommands.ZoomInId);
        await RunAsync(DiagramCommands.ZoomToFitId);

        Assert.Equal((1.1, 1), (preview.Zoom, fits));
    }

    // Keyboard and mouse on equal terms: every command has a shortcut or a menu item, and all are in the palette.
    [Fact]
    public void Commands_HaveKeysAndMenuItems()
    {
        Assert.Equal(KeySequence.Parse("Ctrl+K V"), _keybindings.FindForCommand(DiagramCommands.OpenPreviewToSideId)?.Sequence);
        Assert.Equal(KeySequence.Parse("Ctrl+="), _keybindings.FindForCommand(DiagramCommands.ZoomInId)?.Sequence);
        Assert.Equal(
            [DiagramCommands.OpenPreviewToSideId, DiagramCommands.ExportSvgId, DiagramCommands.ExportPngId],
            _menus.GetItems(EditorCommands.TabContextMenuId).Select(item => item.CommandId));
        Assert.All(_registry.Commands, command => Assert.StartsWith("Схема: ", command.DisplayTitle, StringComparison.Ordinal));
    }

    private async Task RunAsync(string commandId) =>
        Assert.Equal(CommandExecutionStatus.Succeeded, await _service.ExecuteAsync(commandId, null, TestContext.Current.CancellationToken));
}
