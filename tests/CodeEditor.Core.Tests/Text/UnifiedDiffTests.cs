using CodeEditor.Core.Text;

namespace CodeEditor.Core.Tests.Text;

public sealed class UnifiedDiffTests
{
    [Fact]
    public void ChangedLine_WithContext()
    {
        var diff = UnifiedDiff.Format("src/a.cs", "a\nb\nc\nd\ne\nf\ng\nh\n", "a\nb\nc\nd\nE\nf\ng\nh\n");

        Assert.Equal("--- a/src/a.cs\n+++ b/src/a.cs\n@@ -2,7 +2,7 @@\n b\n c\n d\n-e\n+E\n f\n g\n h\n", diff);
    }

    [Fact]
    public void CreatedAndDeletedFiles_UseDevNull()
    {
        Assert.Equal("--- /dev/null\n+++ b/n.cs\n@@ -0,0 +1,2 @@\n+x\n+y\n", UnifiedDiff.Format("n.cs", null, "x\ny\n"));
        Assert.Equal("--- a/o.cs\n+++ /dev/null\n@@ -1,1 +0,0 @@\n-x\n", UnifiedDiff.Format("o.cs", "x", null));
    }

    [Fact]
    public void SameText_GivesEmptyDiff() => Assert.Empty(UnifiedDiff.Format("a.cs", "x\n", "x\n"));
}
