using CodeEditor.Modules.Diagrams.Services;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// <c>```mermaid</c> blocks in Markdown, as on GitHub: CommonMark fenced blocks whose info string starts with
/// <c>mermaid</c>; the diagram's start line maps errors to file lines.
/// </summary>
public sealed class MarkdownMermaidBlocksTests
{
    [Fact]
    public void Blocks_AreFoundInOrder_WithTheirFirstLines()
    {
        const string markdown = "# Архитектура\n\n```mermaid\nflowchart LR\n  A --> B\n```\n\nТекст.\n\n```mermaid\nsequenceDiagram\n  A->>B: Hi\n```\n";

        var blocks = MarkdownMermaidBlocks.Find(markdown);

        Assert.Equal(
            [("flowchart LR\n  A --> B\n", 4, 1), ("sequenceDiagram\n  A->>B: Hi\n", 11, 2)],
            blocks.Select(block => (block.Text, block.FirstLine, block.Index)));
    }

    // A mermaid example inside another code block is not a diagram: only an equal or longer fence closes a fence.
    [Fact]
    public void OtherCodeBlocks_AreSkipped_EvenWhenTheyContainAMermaidFence()
    {
        const string markdown = "````markdown\n```mermaid\ngraph TD\n```\n````\n\n```csharp\nvar x = 1;\n```\n\n```mermaid\npie\n```";

        var block = Assert.Single(MarkdownMermaidBlocks.Find(markdown));

        Assert.Equal(("pie\n", 12), (block.Text, block.FirstLine));
    }

    [Theory]
    [InlineData("~~~mermaid\ngraph TD\n~~~", "graph TD\n")]
    [InlineData("```Mermaid title=\"Схема\"\ngraph TD\n```", "graph TD\n")]
    [InlineData("  ```mermaid\n  graph TD\n    A-->B\n  ```", "graph TD\n  A-->B\n")]
    [InlineData("```mermaid\r\ngraph TD\r\n```\r\n", "graph TD\n")]
    public void FenceVariants_AreRecognized(string markdown, string expected) =>
        Assert.Equal(expected, Assert.Single(MarkdownMermaidBlocks.Find(markdown)).Text);

    [Theory]
    [InlineData("```mermaidx\ngraph TD\n```")]
    [InlineData("    ```mermaid\ngraph TD\n```")]
    [InlineData("``mermaid\ngraph TD\n``")]
    [InlineData("Текст без блоков")]
    public void NotMermaidBlocks_AreIgnored(string markdown) => Assert.Empty(MarkdownMermaidBlocks.Find(markdown));

    // The diagram shows while it's being typed: an unclosed block runs to the end of the file, as in CommonMark.
    [Fact]
    public void UnclosedBlock_RunsToTheEnd()
    {
        var block = Assert.Single(MarkdownMermaidBlocks.Find("Текст\n```mermaid\ngraph TD\n  A-->"));

        Assert.Equal(("graph TD\n  A-->\n", 3), (block.Text, block.FirstLine));
    }

    [Fact]
    public void ShorterFence_DoesNotCloseALongerOne()
    {
        var block = Assert.Single(MarkdownMermaidBlocks.Find("````mermaid\ngraph TD\n```\nA-->B\n````"));

        Assert.Equal("graph TD\n```\nA-->B\n", block.Text);
    }
}
