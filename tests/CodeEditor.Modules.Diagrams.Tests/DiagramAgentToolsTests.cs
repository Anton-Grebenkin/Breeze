using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Diagrams.Services.Agent;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// The <c>diagram</c> tool (ADR 0035) on a fake renderer: check reports errors with file lines, view sends the image to
/// the model in the next message, save writes a file alongside; without a renderer there is no tool.
/// </summary>
public sealed class DiagramAgentToolsTests : IDisposable
{
    private readonly DiagramsFixture _fixture = new();
    private readonly FakeAgentImages _images = new();
    private readonly AIFunction _tool;

    public DiagramAgentToolsTests() => _tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(Tools([_fixture.Renderer]).CreateTools()));

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void WithoutRenderer_ThereIsNoTool() => Assert.Empty(Tools([]).CreateTools());

    [Fact]
    public void Tool_NeedsApproval_SoThatPolicyDecides() => Assert.NotNull(_tool.GetService<ApprovalRequiredAIFunction>());

    [Fact]
    public async Task Check_ValidFile_NamesTheDiagramTypes()
    {
        _fixture.AddFile("docs/README.md", "```mermaid\nflowchart LR\n```\n\n```mermaid\nsequenceDiagram\n```");

        var result = await InvokeAsync(new() { ["action"] = "check", ["path"] = "docs/README.md" });

        Assert.Equal("docs/README.md: ошибок нет (flowchart, sequenceDiagram).", result);
    }

    // The model edits by file lines: a Markdown block error carries the file line, not the diagram line.
    [Fact]
    public async Task Check_Errors_ComeWithFileLines()
    {
        _fixture.AddFile("docs/README.md", "# Заказ\n\n```mermaid\ngraph TD\n  A --> !!\n```");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "check", ["path"] = "docs/README.md" }));

        Assert.StartsWith("Ошибки в схеме docs/README.md:\nСтрока 5: Parse error:", error.Message, StringComparison.Ordinal);
    }

    // Unsaved edits are visible: an open document is read from its buffer.
    [Fact]
    public async Task Check_ReadsTheOpenDocument()
    {
        _fixture.AddFile("a.mmd", "pie");
        var document = await _fixture.OpenAsync("a.mmd");
        document.Buffer.Replace(0, 3, "gantt");

        Assert.Equal("a.mmd: ошибок нет (gantt).", await InvokeAsync(new() { ["action"] = "check", ["path"] = "a.mmd" }));
    }

    [Fact]
    public async Task Check_Text_WithoutAFile()
    {
        Assert.Equal("text: ошибок нет (pie).", await InvokeAsync(new() { ["action"] = "check", ["text"] = "pie\n  \"A\" : 1" }));
        Assert.Equal(["pie\n  \"A\" : 1"], _fixture.Renderer.Checked);
    }

    // A diagram the model wrapped in ```mermaid is checked without the fence; lines are lines of the passed text.
    [Fact]
    public async Task Check_FencedText_ChecksTheBlock()
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "check", ["text"] = "```mermaid\ngraph TD\n  A --> !!\n```" }));

        Assert.Equal(["graph TD\n  A --> !!\n"], _fixture.Renderer.Checked);
        Assert.StartsWith("Ошибки в схеме text:\nСтрока 3:", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task View_ShowsTheModelAPicture_OfTheChosenBlock()
    {
        _fixture.AddFile("docs/README.md", "```mermaid\npie\n```\n\n```mermaid\ngantt\n```");

        var result = await InvokeAsync(new() { ["action"] = "view", ["path"] = "docs/README.md", ["block"] = 2 });

        Assert.Equal("Схема docs/README.md — в следующем сообщении.", result);
        var (name, data, mediaType) = Assert.Single(_images.Shown);
        Assert.Equal(("docs/README.md#2", "image/png"), (name, mediaType));
        Assert.Equal(FakeDiagramRenderer.Png("gantt"), data);
        Assert.Equal(DiagramPngSize.Model, Assert.Single(_fixture.Renderer.Rasterized).Size);
    }

    [Fact]
    public async Task View_ForAModelWithoutVision_SaysToCheck()
    {
        _images.CanShow = false;

        var result = await InvokeAsync(new() { ["action"] = "view", ["text"] = "pie" });

        Assert.Contains("check", result, StringComparison.Ordinal);
        Assert.Empty(_fixture.Renderer.Rasterized);
    }

    // Images within the model's limit are shown; the response tells the model about the one that didn't fit.
    [Fact]
    public async Task View_ShowsWhatFits_AndNamesTheRest()
    {
        _fixture.AddFile("docs/README.md", "```mermaid\npie\n```\n\n```mermaid\ngantt\n```");
        _images.MaxBytes = FakeDiagramRenderer.Png("pie").Length;

        var result = await InvokeAsync(new() { ["action"] = "view", ["path"] = "docs/README.md" });

        Assert.Equal("Схема docs/README.md — в следующем сообщении. Не показаны — больше предела модели: docs/README.md#2.", result);
        Assert.Equal("docs/README.md#1", Assert.Single(_images.Shown).Name);
    }

    [Fact]
    public async Task View_TooLargeForTheModel_IsExplained()
    {
        _images.MaxBytes = 1;

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "view", ["text"] = "pie" }));

        Assert.Contains("больше предела модели", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_SavesNextToTheFile()
    {
        _fixture.AddFile("docs/arch.mmd", "graph TD");

        var result = await InvokeAsync(new() { ["action"] = "render", ["path"] = "docs/arch.mmd", ["format"] = "png" });

        Assert.Equal("Сохранено: docs/arch.png.", result);
        Assert.Equal(FakeDiagramRenderer.Png("graph TD"), _fixture.FileSystem.ReadAllBytes(DiagramsFixture.PathOf("docs/arch.png")));
    }

    [Theory]
    [InlineData("render", null, null, null, "path")]
    [InlineData("render", "docs/arch.mmd", null, "gif", "gif")]
    [InlineData("draw", "docs/arch.mmd", null, null, "check, view, render")]
    [InlineData("check", null, null, null, "path")]
    [InlineData("check", "docs/arch.cs", null, null, "не файл схем")]
    [InlineData("check", "docs/missing.mmd", null, null, "не найден")]
    [InlineData("check", "docs/arch.mmd", 3, null, "нет блока 3")]
    [InlineData("check", "../outside.mmd", null, null, "outside.mmd")]
    public async Task BadCalls_AreExplained(string action, string? path, int? block, string? format, string expected)
    {
        _fixture.AddFile("docs/arch.mmd", "graph TD").AddFile("docs/arch.cs", "class A {}");

        var error = await Assert.ThrowsAsync<AgentToolException>(() =>
            InvokeAsync(new() { ["action"] = action, ["path"] = path, ["block"] = block, ["format"] = format }));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendererFailure_GoesToTheModel()
    {
        _fixture.Renderer.Failure = new DiagramRendererException("Mermaid не загрузился (нет сети).");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "check", ["text"] = "pie" }));

        Assert.Equal("Mermaid не загрузился (нет сети).", error.Message);
    }

    private DiagramAgentTools Tools(IEnumerable<IDiagramRenderer> renderers) => new(
        renderers,
        new DiagramToolInputs(_fixture.Workspace, _fixture.Reader),
        new DiagramRenderTargets(_fixture.Workspace, _fixture.FileSystem, _fixture.Writes),
        _fixture.FileSystem,
        _fixture.Workspace,
        _fixture.Writes,
        _images);

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
}
