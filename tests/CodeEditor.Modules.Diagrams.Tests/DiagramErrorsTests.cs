using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Diagram errors use file lines: a Markdown block starts mid-file, so Mermaid's "on line 2" is removed and the caption
/// names the file line. The snippet with "^" and the expected tokens stay.
/// </summary>
public sealed class DiagramErrorsTests
{
    private const string MermaidMessage = "Parse error on line 2:\n...A --> \n-------^\nExpecting 'NODE_STRING', got 'EOF'";

    [Fact]
    public void Problem_InAMarkdownBlock_NamesTheFileLine()
    {
        var problem = DiagramErrors.Locate(new DiagramSource("graph TD\n  A -->", FirstLine: 13, Index: 2), new DiagramError(MermaidMessage, 2));

        Assert.Equal((2, 14), (problem.Index, problem.Line));
        Assert.Equal("Строка 14: Parse error:\n...A --> \n-------^\nExpecting 'NODE_STRING', got 'EOF'", DiagramErrors.Describe(problem));
    }

    [Fact]
    public void Brief_TakesTheHeaderAndWhatMermaidExpected()
    {
        var problem = DiagramErrors.Locate(new DiagramSource("graph TD", 1, 1), new DiagramError(MermaidMessage, 2));

        Assert.Equal("Строка 2: Parse error: Expecting 'NODE_STRING', got 'EOF'", DiagramErrors.Brief(problem));
    }

    [Fact]
    public void EmptyMessage_IsExplained()
    {
        var problem = DiagramErrors.Locate(new DiagramSource("graph TD", 1, 1), new DiagramError("  ", null));

        Assert.Equal("Строка 1: Mermaid не смог отрисовать схему.", DiagramErrors.Describe(problem));
    }
}
