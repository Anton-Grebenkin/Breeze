using CodeEditor.Core.Files;

namespace CodeEditor.Core.Tests.Files;

public sealed class PathExclusionsTests
{
    [Theory]
    [InlineData(".git", true, true)]
    [InlineData("src/bin", true, true)]
    [InlineData("node_modules", true, true)]
    [InlineData("src/Program.cs", false, false)]
    [InlineData("bin", false, false)]
    public void DefaultNames_HideServiceFoldersOnly(string path, bool isDirectory, bool excluded)
    {
        Assert.Equal(excluded, PathExclusions.Empty.IsExcluded(path, isDirectory));
    }

    [Fact]
    public void DefaultNames_ApplyToAncestors()
    {
        Assert.True(PathExclusions.Empty.IsExcluded("src/obj/Debug/app.dll", isDirectory: false));
        Assert.False(PathExclusions.Empty.IsExcluded("src/Program.cs", isDirectory: false));
        Assert.False(PathExclusions.Empty.IsExcluded("docs/bin.md", isDirectory: false));
    }

    [Theory]
    [InlineData("*.log", "app.log", false, true)]
    [InlineData("*.log", "logs/deep/app.log", false, true)]
    [InlineData("*.log", "app.log.txt", false, false)]
    [InlineData("/build", "build", true, true)]
    [InlineData("/build", "src/build", true, false)]
    [InlineData("out/", "out", true, true)]
    [InlineData("out/", "out", false, false)]
    [InlineData("out/", "src/out/file.cs", false, true)]
    [InlineData("docs/*.tmp", "docs/a.tmp", false, true)]
    [InlineData("docs/*.tmp", "docs/sub/a.tmp", false, false)]
    [InlineData("docs/**/*.tmp", "docs/sub/deep/a.tmp", false, true)]
    [InlineData("**/cache", "a/b/cache", true, true)]
    [InlineData("secret?.txt", "secret1.txt", false, true)]
    [InlineData("*.LOG", "app.log", false, true)]
    public void GitIgnoreRules_MatchLikeGit(string rule, string path, bool isDirectory, bool excluded)
    {
        Assert.Equal(excluded, PathExclusions.FromGitIgnore(rule).IsExcluded(path, isDirectory));
    }

    [Fact]
    public void Negation_ReincludesPath_LastRuleWins()
    {
        var exclusions = PathExclusions.FromGitIgnore("*.log\n!keep.log\n");

        Assert.True(exclusions.IsExcluded("app.log", isDirectory: false));
        Assert.False(exclusions.IsExcluded("keep.log", isDirectory: false));
    }

    [Fact]
    public void CommentsAndBlankLines_AreIgnored()
    {
        var exclusions = PathExclusions.FromGitIgnore("# комментарий\r\n\r\n   \r\n*.tmp\r\n");

        Assert.True(exclusions.IsExcluded("a.tmp", isDirectory: false));
        Assert.False(exclusions.IsExcluded("# комментарий", isDirectory: false));
    }
}
