using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.TextEditor.Services.Agent;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class EditorToolPresenterTests
{
    private readonly EditorToolPresenter _presenter = new();

    [Fact]
    public void ApplyEdits_OneFile_CountsChangedLines()
    {
        var edits = new[] { new FileEdit("src/Order.cs", "if (a)\n{\n    x();\n}", "if (a && b)\n{\n    x();\n    y();\n}") };

        var view = _presenter.Present(new AgentToolCall(EditingAgentTools.ApplyEditsName, new Dictionary<string, object?> { ["edits"] = edits }, "Изменены файлы: src/Order.cs."))!;

        Assert.Equal(("Изменён Order.cs", "+2 −1", "src/Order.cs", AgentToolIcon.Edit), (view.Title, view.Detail, view.FilePath, view.Icon));
    }

    [Fact]
    public void ApplyEdits_SeveralFiles_WhileRunning()
    {
        var edits = new[] { new FileEdit("a.cs", "x", "y"), new FileEdit("b.cs", "x", "y"), new FileEdit("a.cs", "z", "w") };

        var view = _presenter.Present(new AgentToolCall(EditingAgentTools.ApplyEditsName, new Dictionary<string, object?> { ["edits"] = edits }))!;

        Assert.Equal(("Правка 2 файла", "+3 −3"), (view.Title, view.Detail));
    }

    [Fact]
    public void ApplyPatch_CountsFilesAndLines()
    {
        const string input = "*** Begin Patch\n*** Update File: src/Order.cs\n@@ class Order\n a();\n-b();\n+c();\n+d();\n*** End Patch";

        var view = _presenter.Present(new AgentToolCall(EditingAgentTools.ApplyPatchName, new Dictionary<string, object?> { ["input"] = input }, "ok"))!;

        Assert.Equal(("Изменён Order.cs", "+2 −1", "src/Order.cs"), (view.Title, view.Detail, view.FilePath));
    }

    [Fact]
    public void CreateFile_CountsLines()
    {
        var view = _presenter.Present(new AgentToolCall(EditingAgentTools.CreateFileName, new Dictionary<string, object?> { ["path"] = "tests/NewTests.cs", ["content"] = "a\nb\nc\n" }, "Создан файл tests/NewTests.cs."))!;

        Assert.Equal(("Создан NewTests.cs", "+3"), (view.Title, view.Detail));
    }

    [Theory]
    [InlineData("", "", 0, 0)]
    [InlineData("a\nb", "a\nb", 0, 0)]
    [InlineData("a\nb", "a\nc\nd", 2, 1)]
    [InlineData("x\r\ny\r\n", "x\ny", 0, 0)]
    public void LineDiff_MatchesLinesAsMultisets(string oldText, string newText, int added, int removed) =>
        Assert.Equal((added, removed), LineDiff.Count(oldText, newText));
}
