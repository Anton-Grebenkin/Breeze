using CodeEditor.Shell.Instances;

namespace CodeEditor.Shell.Tests.Instances;

public sealed class OpenWithFileTypesTests
{
    // Windows matches extensions ignoring case, so a duplicate in another case would register twice.
    [Fact]
    public void Extensions_AreLowercaseDottedAndDistinct()
    {
        Assert.All(OpenWithFileTypes.Extensions, extension => Assert.Matches("^\\.[a-z0-9]+$", extension));
        Assert.Equal(OpenWithFileTypes.Extensions.Length, OpenWithFileTypes.Extensions.Distinct().Count());
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".cs")]
    [InlineData(".csproj")]
    [InlineData(".json")]
    [InlineData(".md")]
    public void Extensions_CoverEverydayFiles(string extension) => Assert.Contains(extension, OpenWithFileTypes.Extensions);
}
