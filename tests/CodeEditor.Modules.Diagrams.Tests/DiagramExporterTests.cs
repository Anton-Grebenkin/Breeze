using System.Text;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Export next to the source: a Mermaid file gets a same-named image, Markdown blocks get numbered ones; light theme;
/// a diagram with an error is skipped while the others are written; written files are tracked by
/// <see cref="DiagramWrites"/>.
/// </summary>
public sealed class DiagramExporterTests : IDisposable
{
    private readonly DiagramsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task MermaidFile_IsSavedAsSvg_InTheLightTheme()
    {
        var source = DiagramsFixture.PathOf("docs/arch.mmd");

        var export = await _fixture.Exporter.ExportAsync(source, DiagramFiles.Extract(source, "graph TD"), DiagramFormat.Svg, TestContext.Current.CancellationToken);

        var target = DiagramsFixture.PathOf("docs/arch.svg");
        Assert.Equal([target], export.Written);
        Assert.Empty(export.Problems);
        Assert.Equal(FakeDiagramRenderer.Svg("graph TD", DiagramTheme.Light), Encoding.UTF8.GetString(_fixture.FileSystem.ReadAllBytes(target)));
        Assert.True(_fixture.Writes.Contains(target));
    }

    [Fact]
    public async Task MarkdownBlocks_AreNumbered_AndBrokenOnesAreSkipped()
    {
        var source = DiagramsFixture.PathOf("README.md");
        var diagrams = DiagramFiles.Extract(source, "```mermaid\npie\n```\n\n```mermaid\ngraph TD\n  A --> !!\n```\n\n```mermaid\nsequenceDiagram\n```");

        var export = await _fixture.Exporter.ExportAsync(source, diagrams, DiagramFormat.Png, TestContext.Current.CancellationToken);

        Assert.Equal([DiagramsFixture.PathOf("README-1.png"), DiagramsFixture.PathOf("README-3.png")], export.Written);
        var problem = Assert.Single(export.Problems);
        Assert.Equal((2, 7), (problem.Index, problem.Line));
        Assert.False(_fixture.FileSystem.FileExists(DiagramsFixture.PathOf("README-2.png")));
        Assert.Equal("Сохранено: README-1.png, README-3.png. Не сохранено: Строка 7: Parse error: Expecting 'NODE', got '!!'", DiagramExports.Summary(export));
    }

    [Theory]
    [InlineData("docs/a.mmd", DiagramFormat.Svg, 1, "docs/a.svg")]
    [InlineData("docs/a.mermaid", DiagramFormat.Png, 1, "docs/a.png")]
    [InlineData("docs/guide.md", DiagramFormat.Svg, 2, "docs/guide-2.svg")]
    public void ExportPaths_AreNextToTheSource(string source, DiagramFormat format, int index, string expected) =>
        Assert.Equal(DiagramsFixture.PathOf(expected), DiagramExportPaths.For(DiagramsFixture.PathOf(source), format, index));

    [Fact]
    public async Task FileWithoutDiagrams_SaysSo()
    {
        await _fixture.Exports.ExportAsync(DiagramsFixture.PathOf("README.md"), () => Task.FromResult("# Без схем"), DiagramFormat.Svg);

        Assert.Equal("В файле нет схем Mermaid.", _fixture.StatusBar.Message);
    }
}
