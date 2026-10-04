using CodeEditor.Modules.Agent.Contracts.Text;

namespace CodeEditor.Modules.Agent.Tests.Rules;

public sealed class FrontMatterTests
{
    [Fact]
    public void ReadsScalarsListsAndMaps()
    {
        var header = FrontMatter.Parse("""
            ---
            # comment
            description: "Count lines" # trailing comment
            ask_before: [new-package, 'delete-tests']
            protected:
              - "src/**"
              - .github/**
            parameters:
              path: File to count, relative to the root
              pattern: "Mask: *.cs"
            ---
            # Body
            text
            """);

        Assert.Empty(header.Errors);
        Assert.Equal("Count lines", header.Text("description"));
        Assert.Equal(["new-package", "delete-tests"], header.List("ask_before"));
        Assert.Equal(["src/**", ".github/**"], header.List("protected"));
        Assert.Equal(
            [new("path", "File to count, relative to the root"), new("pattern", "Mask: *.cs")],
            header.Map("parameters"));
        Assert.Equal("# Body\ntext", header.Body);
    }

    [Fact]
    public void ScalarList_SplitsByCommas_AndKeepsHashInsideWords()
    {
        var header = FrontMatter.Parse("---\napplies: src/**/*.xaml, *.axaml\nlanguage: C#\n---\n");

        Assert.Equal(["src/**/*.xaml", "*.axaml"], header.List("applies"));
        Assert.Equal("C#", header.Text("language"));
        Assert.Equal(string.Empty, header.Body);
    }

    [Fact]
    public void WithoutHeader_WholeTextIsBody()
    {
        var header = FrontMatter.Parse("# Rules\n- one");

        Assert.Empty(header.Keys);
        Assert.Empty(header.Errors);
        Assert.Equal("# Rules\n- one", header.Body);
    }

    [Fact]
    public void Problems_HaveLineNumbers()
    {
        var header = FrontMatter.Parse("---\nprotected: [a]\nnot a setting\nprotected: [b]\n  orphan: x\n---\nbody");

        Assert.Equal(3, header.Errors.Count);
        Assert.Contains("3", header.Errors[0], StringComparison.Ordinal);
        Assert.Contains("4", header.Errors[1], StringComparison.Ordinal);
        Assert.Contains("5", header.Errors[2], StringComparison.Ordinal);
        Assert.Equal(["a"], header.List("protected"));
    }

    [Fact]
    public void UnclosedHeader_IsReported_AndTextStaysBody()
    {
        var header = FrontMatter.Parse("---\nprotected: [a]\nrules");

        Assert.Single(header.Errors);
        Assert.Empty(header.Keys);
        Assert.StartsWith("---", header.Body, StringComparison.Ordinal);
    }
}
