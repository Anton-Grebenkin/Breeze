using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Mermaid error lines map to the user's text lines: Mermaid strips leading front matter, directives, comments and blank
/// lines and numbers the rest; comments in the middle become blank lines and don't shift numbering.
/// </summary>
public sealed class MermaidSourceTests
{
    [Fact]
    public void PlainDiagram_HasNoShift()
    {
        var prepared = MermaidSource.Prepare("flowchart LR\n  A --> B\n  B --> !!");

        Assert.Equal((0, 3), (prepared.LeadingLines, prepared.LineCount));
        Assert.Equal(3, prepared.TextLine(3));
    }

    [Fact]
    public void FrontMatter_AndBlankLines_ShiftErrorLines()
    {
        var prepared = MermaidSource.Prepare("---\ntitle: Заказ\n---\n\nflowchart LR\n  A --> !!");

        Assert.Equal(4, prepared.LeadingLines);
        Assert.Equal(6, prepared.TextLine(2));
    }

    [Fact]
    public void LeadingCommentsAndDirectives_ShiftErrorLines()
    {
        var prepared = MermaidSource.Prepare("%% схема заказа\n%%{init: {\"theme\": \"forest\"}}%%\n\nsequenceDiagram\n  A->>B: !!");

        Assert.Equal(3, prepared.LeadingLines);
        Assert.Equal(5, prepared.TextLine(2));
    }

    [Fact]
    public void MultilineDirective_AtTheStart_IsCounted()
    {
        var prepared = MermaidSource.Prepare("%%{\n  init: { \"theme\": \"dark\" }\n}%%\ngraph TD\n  A --> !!");

        Assert.Equal(3, prepared.LeadingLines);
        Assert.Equal(5, prepared.TextLine(2));
    }

    // Mermaid removes comment lines with their line break; here they become blank, so line numbers don't shift.
    [Fact]
    public void CommentsInTheMiddle_BecomeBlankLines()
    {
        var prepared = MermaidSource.Prepare("graph TD\r\n  %% пояснение\r\n  A --> B\r\n%%{init: {}}%%");

        Assert.Equal("graph TD\n\n  A --> B\n%%{init: {}}%%", prepared.Text);
        Assert.Equal(4, prepared.LineCount);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(0, 2)]
    [InlineData(99, 3)]
    public void TextLine_WithoutOrBeyondTheText_StaysInside(int? mermaidLine, int expected) =>
        Assert.Equal(expected, MermaidSource.Prepare("\ngraph TD\n  A --> B").TextLine(mermaidLine));
}
