using CodeEditor.Core.Files;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Theming;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Live preview (ADR 0035) on a fake renderer: re-render after a typing pause, errors as text with the file line over
/// the last good image, all Markdown blocks in order, editor theme, closed document.
/// </summary>
public sealed class DiagramPreviewViewModelTests : IDisposable
{
    private readonly DiagramsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task OpenFile_IsRenderedRightAway_InTheEditorTheme()
    {
        _fixture.AddFile("docs/arch.mmd", "flowchart LR\n  A --> B");
        await _fixture.OpenAsync("docs/arch.mmd");

        using var preview = await _fixture.PreviewAsync("docs/arch.mmd");

        var item = Assert.Single(preview.Diagrams);
        Assert.Equal(FakeDiagramRenderer.Svg("flowchart LR\n  A --> B", DiagramTheme.Dark), item.Svg);
        Assert.False(item.IsStale);
        Assert.Null(item.Caption);
        Assert.Equal((DiagramTheme.Dark, null, null), (preview.Theme, preview.Placeholder, preview.Error));
    }

    // An edit re-renders after a pause, not at once; a burst of edits is one render with the latest text.
    [Fact]
    public async Task Typing_RendersOnceAfterThePause()
    {
        _fixture.AddFile("a.mmd", "graph TD");
        var document = await _fixture.OpenAsync("a.mmd");
        using var preview = await _fixture.PreviewAsync("a.mmd");
        _fixture.Renderer.Rendered.Clear();

        document.Buffer.Replace(document.Buffer.Length, 0, "\n  A");
        _fixture.Time.Advance(DiagramPreviewViewModel.TypingDelay / 2);
        document.Buffer.Replace(document.Buffer.Length, 0, " --> B");
        _fixture.Time.Advance(DiagramPreviewViewModel.TypingDelay - TimeSpan.FromMilliseconds(1));

        Assert.Empty(_fixture.Renderer.Rendered);

        await _fixture.AfterTypingAsync(preview);

        Assert.Equal(["graph TD\n  A --> B"], _fixture.Renderer.Rendered.Select(call => call.Text));
        Assert.Equal(FakeDiagramRenderer.Svg("graph TD\n  A --> B", DiagramTheme.Dark), Assert.Single(preview.Diagrams).Svg);
    }

    // While a diagram is being typed, the error is shown as text with its line over the last good image, dimmed.
    [Fact]
    public async Task SyntaxError_KeepsTheLastGoodPicture_AndNamesTheLine()
    {
        _fixture.AddFile("a.mmd", "graph TD\n  A --> B");
        var document = await _fixture.OpenAsync("a.mmd");
        using var preview = await _fixture.PreviewAsync("a.mmd");

        document.Buffer.Replace(document.Buffer.Length, 0, "\n  B --> !!");
        await _fixture.AfterTypingAsync(preview);

        var item = Assert.Single(preview.Diagrams);
        Assert.Equal((FakeDiagramRenderer.Svg("graph TD\n  A --> B", DiagramTheme.Dark), true), (item.Svg, item.IsStale));
        Assert.StartsWith("Строка 3: Parse error:", preview.Error, StringComparison.Ordinal);
        Assert.Equal(3, preview.ErrorLine);

        document.Buffer.Replace(document.Buffer.Length - 2, 2, "C");
        await _fixture.AfterTypingAsync(preview);

        Assert.False(Assert.Single(preview.Diagrams).IsStale);
        Assert.Null(preview.Error);
        Assert.Null(preview.ErrorLine);
    }

    [Fact]
    public async Task ErrorBeforeAnyPicture_ShowsAPlaceholder()
    {
        _fixture.AddFile("a.mmd", "graph TD\n  A --> !!");
        await _fixture.OpenAsync("a.mmd");

        using var preview = await _fixture.PreviewAsync("a.mmd");

        Assert.Empty(preview.Diagrams);
        Assert.Equal("Схема не отрисована: ошибка — выше.", preview.Placeholder);
        Assert.NotNull(preview.Error);
    }

    [Fact]
    public async Task GoToError_OpensTheFileAtTheLine()
    {
        _fixture.AddFile("docs/README.md", "# Заказ\n\n```mermaid\ngraph TD\n  A --> !!\n```\n");
        await _fixture.OpenAsync("docs/README.md");
        using var preview = await _fixture.PreviewAsync("docs/README.md");

        await preview.GoToErrorCommand.ExecuteAsync(null);

        Assert.Equal(
            [(ShellCommandIds.OpenFile, (object?)DiagramsFixture.PathOf("docs/README.md")), (ShellCommandIds.EditorGoToLine, 5)],
            _fixture.Commands.Executed);
    }

    [Fact]
    public async Task Markdown_ShowsEveryBlockInOrder_WithCaptions()
    {
        _fixture.AddFile("README.md", "# Схемы\n\n```mermaid\npie\n```\n\nТекст\n\n```mermaid\nsequenceDiagram\n```\n");
        await _fixture.OpenAsync("README.md");

        using var preview = await _fixture.PreviewAsync("README.md");

        Assert.Equal(
            [(1, "Схема 1 · строка 4", FakeDiagramRenderer.Svg("pie", DiagramTheme.Dark)), (2, "Схема 2 · строка 10", FakeDiagramRenderer.Svg("sequenceDiagram", DiagramTheme.Dark))],
            preview.Diagrams.Select(item => (item.Index, item.Caption, item.Svg)));
    }

    // A new broken block above an old one doesn't get the old image: previous images are matched by diagram text.
    [Fact]
    public async Task NewBrokenBlockAbove_DoesNotBorrowAnotherPicture()
    {
        _fixture.AddFile("README.md", "```mermaid\npie\n```\n");
        var document = await _fixture.OpenAsync("README.md");
        using var preview = await _fixture.PreviewAsync("README.md");

        document.Buffer.Replace(0, 0, "```mermaid\ngraph TD\n  A --> !!\n```\n\n");
        await _fixture.AfterTypingAsync(preview);

        var item = Assert.Single(preview.Diagrams);
        Assert.Equal((2, false, FakeDiagramRenderer.Svg("pie", DiagramTheme.Dark)), (item.Index, item.IsStale, item.Svg));
        Assert.Equal(3, preview.ErrorLine);
    }

    // A file that isn't open changed on disk (git, another editor): the diagram re-renders.
    [Fact]
    public async Task ClosedFile_ChangedOnDisk_IsRedrawn()
    {
        _fixture.AddFile("a.mmd", "pie");
        using var preview = await _fixture.PreviewAsync("a.mmd");

        _fixture.AddFile("a.mmd", "gantt");
        _fixture.FileSystem.Watchers[0].Raise(new FileChange(DiagramsFixture.PathOf("a.mmd"), FileChangeKind.Changed));
        await preview.Pending;

        Assert.Equal(FakeDiagramRenderer.Svg("gantt", DiagramTheme.Dark), Assert.Single(preview.Diagrams).Svg);
    }

    [Fact]
    public async Task UnreadableFile_IsExplained()
    {
        using var preview = new DiagramPreviewViewModel(DiagramsFixture.PathOf("locked.mmd"), _fixture.Services with
        {
            Reader = new DiagramTextReader(_fixture.Documents, new LockedFileSystem(), _fixture.Dispatcher),
        });
        await preview.Pending;

        Assert.Equal("Файл не прочитан: Файл занят другим процессом.", preview.Error);
        Assert.Equal("Схема не отрисована: ошибка — выше.", preview.Placeholder);
    }

    // Editing one block of a large Markdown file renders once: the other blocks didn't change.
    [Fact]
    public async Task Markdown_RendersOnlyTheChangedBlock()
    {
        _fixture.AddFile("README.md", "```mermaid\npie\n```\n\n```mermaid\ngantt\n```\n");
        var document = await _fixture.OpenAsync("README.md");
        using var preview = await _fixture.PreviewAsync("README.md");
        _fixture.Renderer.Rendered.Clear();

        document.Buffer.Replace(document.Buffer.GetText().IndexOf("gantt", StringComparison.Ordinal) + 5, 0, "\n  title План");
        await _fixture.AfterTypingAsync(preview);

        Assert.Equal(["gantt\n  title План\n"], _fixture.Renderer.Rendered.Select(call => call.Text));
        Assert.Equal(2, preview.Diagrams.Length);
    }

    [Theory]
    [InlineData("README.md", "# Без схем", "В файле нет блоков ```mermaid.")]
    [InlineData("empty.mmd", "  \n", "Схема пуста: напишите текст Mermaid, например «flowchart LR» и «A --> B».")]
    public async Task NoDiagrams_ShowsAPlaceholder(string name, string text, string placeholder)
    {
        _fixture.AddFile(name, text);
        await _fixture.OpenAsync(name);

        using var preview = await _fixture.PreviewAsync(name);

        Assert.Empty(preview.Diagrams);
        Assert.Equal(placeholder, preview.Placeholder);
        Assert.Empty(_fixture.Renderer.Rendered);
    }

    [Fact]
    public async Task ThemeChange_RendersInTheNewTheme()
    {
        _fixture.AddFile("a.mmd", "pie");
        await _fixture.OpenAsync("a.mmd");
        using var preview = await _fixture.PreviewAsync("a.mmd");
        var rendered = 0;
        preview.Rendered += (_, _) => rendered++;

        _fixture.Themes.Apply(ThemeKind.Light);
        await preview.Pending;

        Assert.Equal((DiagramTheme.Light, 1), (preview.Theme, rendered));
        Assert.Equal(FakeDiagramRenderer.Svg("pie", DiagramTheme.Light), Assert.Single(preview.Diagrams).Svg);
    }

    [Fact]
    public async Task RendererFailure_IsShownAsTheError()
    {
        _fixture.AddFile("a.mmd", "pie");
        await _fixture.OpenAsync("a.mmd");
        _fixture.Renderer.Failure = new DiagramRendererException("Mermaid не загрузился (нет сети).");

        using var preview = await _fixture.PreviewAsync("a.mmd");

        Assert.Equal("Mermaid не загрузился (нет сети).", preview.Error);
        Assert.Null(preview.ErrorLine);
        Assert.Equal("Схема не отрисована: ошибка — выше.", preview.Placeholder);
    }

    // Closed file: no more edits, the image stays; reopened: the preview follows the text again.
    [Fact]
    public async Task ClosedDocument_KeepsThePicture_AndReattachesOnReopen()
    {
        _fixture.AddFile("a.mmd", "pie");
        var document = await _fixture.OpenAsync("a.mmd");
        using var preview = await _fixture.PreviewAsync("a.mmd");

        await _fixture.Editors.CloseAsync(_fixture.Editors.Find(DiagramsFixture.PathOf("a.mmd")));
        document.Buffer.Replace(0, 0, "%% ");
        _fixture.Time.Advance(DiagramPreviewViewModel.TypingDelay);

        Assert.Single(_fixture.Renderer.Rendered);

        var reopened = await _fixture.OpenAsync("a.mmd");
        await preview.Pending;
        reopened.Buffer.Replace(reopened.Buffer.Length, 0, " title");
        await _fixture.AfterTypingAsync(preview);

        Assert.Equal("pie title", _fixture.Renderer.Rendered[^1].Text);
    }

    [Fact]
    public async Task Export_WritesNextToTheSource_AndReportsIt()
    {
        _fixture.AddFile("docs/arch.mmd", "graph TD");
        await _fixture.OpenAsync("docs/arch.mmd");
        using var preview = await _fixture.PreviewAsync("docs/arch.mmd");

        await preview.ExportPngCommand.ExecuteAsync(null);

        Assert.Equal(FakeDiagramRenderer.Png("graph TD"), _fixture.FileSystem.ReadAllBytes(DiagramsFixture.PathOf("docs/arch.png")));
        Assert.Equal("Сохранено: arch.png.", _fixture.StatusBar.Message);
        Assert.Equal(DiagramPngSize.Export, Assert.Single(_fixture.Renderer.Rasterized).Size);
    }

    [Fact]
    public async Task Zoom_GoesByStepsAndStaysInBounds()
    {
        _fixture.AddFile("a.mmd", "pie");
        using var preview = await _fixture.PreviewAsync("a.mmd");

        preview.ZoomInCommand.Execute(null);
        Assert.Equal(1.1, preview.Zoom);
        preview.PressKey(DiagramPageMessages.ZoomOutKey);
        preview.PressKey(DiagramPageMessages.ZoomOutKey);
        Assert.Equal(0.9, preview.Zoom);

        preview.ReportZoom(1.234);
        preview.ZoomInCommand.Execute(null);
        Assert.Equal(1.25, preview.Zoom);

        preview.ReportZoom(100);
        Assert.Equal(DiagramZoom.Max, preview.Zoom);
        preview.ResetZoomCommand.Execute(null);
        Assert.StartsWith("100", preview.ZoomText, StringComparison.Ordinal);
        Assert.EndsWith("%", preview.ZoomText, StringComparison.Ordinal);
    }
}
