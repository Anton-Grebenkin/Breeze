using CodeEditor.Core.Files;

namespace CodeEditor.Core.Tests.Files;

public sealed class GlobFilterTests
{
    [Theory]
    [InlineData("*.cs", "src/Program.cs", true)]
    [InlineData("*.cs", "Program.cs", true)]
    [InlineData("*.cs", "src/Program.csproj", false)]
    [InlineData("src", "src/Utils/Helper.cs", true)]
    [InlineData("src/", "src/Utils/Helper.cs", true)]
    [InlineData("src/", "tests/src.cs", false)]
    [InlineData("./src", "src/a.cs", true)]
    [InlineData("Utils", "src/Utils/Helper.cs", true)]
    [InlineData("src/**/*.xaml", "src/Views/Main.xaml", true)]
    [InlineData("src/**/*.xaml", "tests/Main.xaml", false)]
    [InlineData("*.Designer.cs", "Forms/Main.Designer.cs", true)]
    [InlineData("*.cs, *.xaml", "App.xaml", true)]
    [InlineData("*.CS", "a.cs", true)]
    [InlineData("*.{cs,sql}", "db/Migrations/001.sql", true)]
    [InlineData("*.{cs,sql}", "src/a.cs", true)]
    [InlineData("*.{cs,sql}", "src/a.json", false)]
    [InlineData("**/*.{cs,sql}, docs/", "docs/readme.md", true)]
    [InlineData("src/{Api,Core}/**", "src/Core/a.cs", true)]
    [InlineData("src/{Api,Core}/**", "src/Web/a.cs", false)]
    [InlineData("*.{cs", "a.{cs", true)]
    public void Matches_FileOrAnyParentFolder(string patterns, string path, bool expected) =>
        Assert.Equal(expected, GlobFilter.Parse(patterns).Matches(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void Parse_Blank_IsEmpty(string? patterns)
    {
        var filter = GlobFilter.Parse(patterns);

        Assert.True(filter.IsEmpty);
        Assert.False(filter.Matches("a.cs"));
    }

    [Theory]
    [InlineData("*.{cs,sql}", "*.cs|*.sql")]
    [InlineData("src/{a,b}/*.{cs,md}", "src/a/*.cs|src/a/*.md|src/b/*.cs|src/b/*.md")]
    [InlineData("{a,{b,c}}", "a|b|c")]
    [InlineData("plain", "plain")]
    [InlineData("broken{", "broken{")]
    public void Expand_Braces(string pattern, string expected) =>
        Assert.Equal(expected.Split('|'), GlobBraces.Expand(pattern));

    [Fact]
    public void Split_IgnoresCommasInsideBraces() =>
        Assert.Equal(["*.{cs,sql}", " docs/"], GlobBraces.Split("*.{cs,sql}, docs/"));
}
