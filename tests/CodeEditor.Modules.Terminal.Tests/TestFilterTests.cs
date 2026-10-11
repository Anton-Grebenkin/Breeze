using CodeEditor.Modules.Terminal.Services.Build;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>The run_tests filter: a name part, several parts, or a VSTest expression typed from habit.</summary>
public sealed class TestFilterTests
{
    [Fact]
    public void Parts_DropPropertyPrefixes_AndDuplicates() =>
        Assert.Equal(["OrderTests", "Cart.Total"], TestFilter.Parts("FullyQualifiedName~OrderTests | Name=Cart.Total|OrderTests"));

    [Fact]
    public void TestingPlatform_GetsFilterMethodPerPart() =>
        Assert.Equal(["--filter-method", "*OrderTests*", "--filter-method", "*CartTests*"], TestFilter.Arguments("FullyQualifiedName~OrderTests|CartTests", testingPlatform: true));

    [Fact]
    public void VsTest_GetsOneContainsExpression() =>
        Assert.Equal(["--filter", "FullyQualifiedName~OrderTests|FullyQualifiedName~CartTests"], TestFilter.Arguments("OrderTests|DisplayName~CartTests", testingPlatform: false));

    [Fact]
    public void VsTest_ExpressionWithOperators_PassesAsIs() =>
        Assert.Equal(["--filter", "Category=Unit&FullyQualifiedName!~Slow"], TestFilter.Arguments("Category=Unit&FullyQualifiedName!~Slow", testingPlatform: false));

    [Theory]
    [InlineData(@"No test matches the given testcase filter `FullyQualifiedName~X` in C:\repo\App.Tests.dll")]
    [InlineData("Test run summary: Zero tests ran - App.Tests.dll\n  total: 0\n  failed: 0\n  succeeded: 0\n  skipped: 0")]
    public void NothingMatched_IsNoTests_NotASuccess(string output)
    {
        var report = TestOutputParser.Parse("App.Tests", output);

        Assert.True(report.NoTests);
        Assert.False(report.Succeeded);
    }
}
