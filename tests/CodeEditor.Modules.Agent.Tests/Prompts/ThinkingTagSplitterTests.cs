using CodeEditor.Modules.Agent.Services.Prompts;

namespace CodeEditor.Modules.Agent.Tests.Prompts;

/// <summary>
/// Tagged think-aloud reasoning in a text stream: a tag may arrive in pieces, text outside tags is the answer, and an
/// unclosed tag is closed at the end of the answer (ADR 0010).
/// </summary>
public sealed class ThinkingTagSplitterTests
{
    [Fact]
    public void TagsSplitAcrossChunks_ThinkingAndTextSeparated()
    {
        var splitter = new ThinkingTagSplitter();
        string[] chunks = ["<thi", "nking>\nСмотрю ", "Reserve.</thi", "nking>\n\nИсправляю ", "отмену."];

        var parts = chunks.SelectMany(splitter.Push).Concat(splitter.Flush()).ToList();

        Assert.Equal("Смотрю Reserve.", Join(parts, TaggedTextKind.Thinking));
        Assert.Equal("Исправляю отмену.", Join(parts, TaggedTextKind.Text));
        Assert.Single(parts, part => part.Kind == TaggedTextKind.ThinkingDone);
    }

    [Fact]
    public void ThinkTag_AndUnclosedThinking_EndedByFlush()
    {
        var splitter = new ThinkingTagSplitter();

        var parts = splitter.Push("<think>план").Concat(splitter.Flush()).ToList();

        Assert.Equal("план", Join(parts, TaggedTextKind.Thinking));
        Assert.Equal(TaggedTextKind.ThinkingDone, parts[^1].Kind);
    }

    [Fact]
    public void TextWithoutTags_PassesThrough_EvenWithAngleBrackets()
    {
        var splitter = new ThinkingTagSplitter();

        var parts = splitter.Push("List<int> и a < b").Concat(splitter.Flush()).ToList();

        Assert.Equal("List<int> и a < b", Join(parts, TaggedTextKind.Text));
        Assert.DoesNotContain(parts, part => part.Kind != TaggedTextKind.Text);
    }

    private static string Join(IEnumerable<TaggedText> parts, TaggedTextKind kind) =>
        string.Concat(parts.Where(part => part.Kind == kind).Select(part => part.Text));
}
