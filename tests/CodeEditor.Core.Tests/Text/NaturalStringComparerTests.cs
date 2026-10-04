using CodeEditor.Core.Text;

namespace CodeEditor.Core.Tests.Text;

public sealed class NaturalStringComparerTests
{
    [Fact]
    public void Sort_OrdersNumbersByValue()
    {
        string[] names = ["file10.cs", "file2.cs", "File1.cs", "file02.cs", "readme.md", "a", "file1a.cs"];

        var sorted = names.Order(NaturalStringComparer.Instance).ToArray();

        Assert.Equal(["a", "File1.cs", "file1a.cs", "file2.cs", "file02.cs", "file10.cs", "readme.md"], sorted);
    }

    [Theory]
    [InlineData("abc", "ABC", 0)]
    [InlineData("a2", "a10", -1)]
    [InlineData("a10", "a2", 1)]
    [InlineData("a", "ab", -1)]
    [InlineData("v1.10", "v1.9", 1)]
    [InlineData("x99999999999999999999", "x100000000000000000000", -1)]
    public void Compare_ReturnsExpectedSign(string left, string right, int expected)
    {
        Assert.Equal(expected, Math.Sign(NaturalStringComparer.Instance.Compare(left, right)));
    }

    [Fact]
    public void Compare_HandlesNulls()
    {
        Assert.Equal(0, NaturalStringComparer.Instance.Compare(null, null));
        Assert.True(NaturalStringComparer.Instance.Compare(null, "a") < 0);
        Assert.True(NaturalStringComparer.Instance.Compare("a", null) > 0);
    }
}
