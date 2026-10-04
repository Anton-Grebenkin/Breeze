using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.TextEditor.Services.Agent;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class PatchParserTests
{
    [Fact]
    public void Update_HunksBecomeEdits_WithAnchors()
    {
        var patch = PatchParser.Parse("""
            *** Begin Patch
            *** Update File: src/A.cs
            @@ class A
                 void Run() { }
            -    void Stop() { }
            +    void Halt() { }
            @@ class B
            @@     void Go()
            -        return;
            +        Run();
            *** End Patch
            """);

        Assert.Empty(patch.Additions);
        Assert.Collection(patch.Edits,
            edit => Assert.Equal(("src/A.cs", "    void Run() { }\n    void Stop() { }", "    void Run() { }\n    void Halt() { }", "class A"),
                (edit.Path, edit.OldText, edit.NewText, string.Join('|', edit.Anchors))),
            edit => Assert.Equal(("        return;", "        Run();", "class B|void Go()"),
                (edit.OldText, edit.NewText, string.Join('|', edit.Anchors))));
    }

    [Fact]
    public void AddFile_CollectsPlusLines()
    {
        var patch = PatchParser.Parse("*** Begin Patch\n*** Add File: tests/NewTests.cs\n+namespace T;\n+\n+public sealed class NewTests { }\n*** End Patch");

        var addition = Assert.Single(patch.Additions);
        Assert.Equal(("tests/NewTests.cs", "namespace T;\n\npublic sealed class NewTests { }\n"), (addition.Path, addition.Content));
    }

    [Fact]
    public void PureInsertion_GoesAfterAnchorLine()
    {
        var edit = Assert.Single(PatchParser.Parse("*** Begin Patch\n*** Update File: a.cs\n@@ class A\n@@ {\n+    int x;\n*** End Patch").Edits);

        Assert.Equal(("{", "{\n    int x;", "class A"), (edit.OldText, edit.NewText, string.Join('|', edit.Anchors)));
    }

    [Fact]
    public void BlankContextWithoutSpace_AndMissingEnd_AreTolerated()
    {
        var edit = Assert.Single(PatchParser.Parse("*** Begin Patch\n*** Update File: a.cs\n a();\n\n-b();\n+c();").Edits);

        Assert.Equal(("a();\n\nb();", "a();\n\nc();"), (edit.OldText, edit.NewText));
    }

    [Fact]
    public void ContextOnlyHunk_IsNotAnEdit_AndEmptyPatchFails() =>
        Assert.Throws<AgentToolException>(() => PatchParser.Parse("*** Begin Patch\n*** Update File: a.cs\n a();\n*** End Patch"));

    [Theory]
    [InlineData("*** Begin Patch\n*** Delete File: a.cs\n*** End Patch", "delete_file")]
    [InlineData("*** Begin Patch\n*** Update File: a.cs\n*** Move to: b.cs\n-a\n+b\n*** End Patch", "move_file")]
    [InlineData("просто текст", "*** Begin Patch")]
    [InlineData("*** Begin Patch\nпривет\n*** End Patch", "Непонятная строка")]
    [InlineData("*** Begin Patch\n*** Update File: a.cs\n+x\n*** End Patch", "нет контекста")]
    public void Unsupported_OrBroken_ExplainsWhatToDo(string input, string hint) =>
        Assert.Contains(hint, Assert.Throws<AgentToolException>(() => PatchParser.Parse(input)).Message, StringComparison.Ordinal);
}
