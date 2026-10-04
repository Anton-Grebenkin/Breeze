using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class EditingAgentToolsTests : IDisposable
{
    private readonly EditorFixture _fixture = new();
    private readonly AgentFileState _fileState = new();
    private readonly EditingAgentTools _tools;
    private readonly Dictionary<string, AIFunction> _functions;

    public EditingAgentToolsTests()
    {
        _fixture.FileSystem
            .AddFile(EditorFixture.PathOf("src/A.cs"), "class A\r\n{\r\n    void Run() { }\r\n    void Stop() { }\r\n}\r\n")
            .AddFile(EditorFixture.PathOf("src/B.cs"), "class B { void Go() => new A().Run(); }\n");
        var planner = new EditPlanner(_fixture.Workspace, _fixture.FileSystem, _fixture.Documents, _fixture.Dispatcher, _fileState);
        _tools = new EditingAgentTools(planner, _fixture.Editors, _fixture.Workspace, _fixture.FileSystem, _fixture.Dispatcher, _fileState, _fixture.Documents);
        MarkRead("src/A.cs");
        MarkRead("src/B.cs");
        _functions = _tools.CreateTools().OfType<AIFunction>().ToDictionary(tool => tool.Name);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void EditingTools_RequireApproval() =>
        Assert.All(_functions.Values, function => Assert.IsType<ApprovalRequiredAIFunction>(function));

    [Fact]
    public async Task ApplyEdits_AcrossFiles_OneUndoPerFile_CrlfKept()
    {
        var result = await Invoke(EditingAgentTools.ApplyEditsName, Edits(
            ("src/A.cs", "    void Run() { }\n    void Stop() { }", "    void Execute() { }\n    void Stop() { }"),
            ("src/B.cs", ".Run()", ".Execute()")));

        Assert.StartsWith("Изменены файлы: src/A.cs, src/B.cs", result, StringComparison.Ordinal);
        var a = Tab("A.cs");
        Assert.Equal("class A\r\n{\r\n    void Execute() { }\r\n    void Stop() { }\r\n}\r\n", a.Document.Buffer.GetText());
        // Saved right away: builds see the edit, and it stays highlighted until reviewed (ADR 0040).
        Assert.False(a.IsDirty);
        Assert.Equal(a.Document.Buffer.GetText(), _fixture.FileSystem.ReadAllText(EditorFixture.PathOf("src/A.cs")));
        Assert.Equal("class B { void Go() => new A().Execute(); }\n", Tab("B.cs").Document.Buffer.GetText());

        a.Document.Buffer.Undo();
        Assert.Equal("class A\r\n{\r\n    void Run() { }\r\n    void Stop() { }\r\n}\r\n", a.Document.Buffer.GetText());
        Assert.False(a.Document.Buffer.CanUndo);
    }

    [Fact]
    public async Task ApplyEdits_UsesUnsavedTextAsBase()
    {
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(EditorFixture.PathOf("src/B.cs")));
        tab!.Document.Buffer.Replace(0, 0, "// правка пользователя\n");

        // The file changed after the agent read it: an edit against stale text fails, after re-reading it succeeds.
        var stale = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(EditingAgentTools.ApplyEditsName, Edits(("src/B.cs", "class B", "sealed class B"))));
        Assert.Contains("изменился после вашего последнего чтения", stale.Message, StringComparison.Ordinal);
        _fileState.RecordRead(EditorFixture.PathOf("src/B.cs"), tab.Document.Buffer.GetText(), 1, 2);

        await Invoke(EditingAgentTools.ApplyEditsName, Edits(("src/B.cs", "class B", "sealed class B")));

        Assert.Equal("// правка пользователя\nsealed class B { void Go() => new A().Run(); }\n", tab.Document.Buffer.GetText());
    }

    [Theory]
    [InlineData("void Nope()", "не найден")]
    [InlineData("void", "встречается несколько раз (строки 3, 4)")]
    [InlineData("", "пустой oldText")]
    public async Task ApplyEdits_BadFragment_IsExplained(string oldText, string expected)
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(EditingAgentTools.ApplyEditsName, Edits(("src/A.cs", oldText, "x"))));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Empty(_fixture.Editors.Tabs);
    }

    [Fact]
    public async Task ApplyEdits_OverlappingEdits_AreRejected() =>
        Assert.Contains("пересекаются", (await Assert.ThrowsAsync<AgentToolException>(() => Invoke(EditingAgentTools.ApplyEditsName,
            Edits(("src/A.cs", "void Run()", "a"), ("src/A.cs", "Run() { }", "b"))))).Message, StringComparison.Ordinal);

    [Fact]
    public async Task CreateFile_WritesAndOpens_ButNotOverExisting()
    {
        await Invoke(EditingAgentTools.CreateFileName, new() { ["path"] = "src/New/C.cs", ["content"] = "class C { }\n" });

        Assert.Equal("class C { }\n", _fixture.FileSystem.ReadAllText(EditorFixture.PathOf("src/New/C.cs")));
        Assert.NotNull(Tab("C.cs"));
        Assert.Contains("уже существует", (await Assert.ThrowsAsync<AgentToolException>(() =>
            Invoke(EditingAgentTools.CreateFileName, new() { ["path"] = "src/A.cs", ["content"] = "x" }))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyEdits_UnreadFile_AskToReadFirst()
    {
        _fixture.FileSystem.AddFile(EditorFixture.PathOf("src/C.cs"), "class C { }\n");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(EditingAgentTools.ApplyEditsName, Edits(("src/C.cs", "class C", "sealed class C"))));

        Assert.Contains("ещё не прочитан", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatedFile_CanBeEdited_WithoutReading()
    {
        await Invoke(EditingAgentTools.CreateFileName, new() { ["path"] = "src/D.cs", ["content"] = "class D { }\n" });

        await Invoke(EditingAgentTools.ApplyEditsName, Edits(("src/D.cs", "class D", "sealed class D")));

        Assert.Equal("sealed class D { }\n", Tab("D.cs").Document.Buffer.GetText());
    }

    [Theory]
    [InlineData("bin/Debug/out.txt", "служебная папка")]
    [InlineData("obj/x.cs", "служебная папка")]
    [InlineData(".env", "секретами")]
    public async Task CreateFile_InProtectedPlaces_IsRefused(string path, string expected) =>
        Assert.Contains(expected, (await Assert.ThrowsAsync<AgentToolException>(() =>
            Invoke(EditingAgentTools.CreateFileName, new() { ["path"] = path, ["content"] = "x" }))).Message, StringComparison.Ordinal);

    [Fact]
    public async Task ApplyEdits_TooLarge_AsksToSplit() =>
        Assert.Contains("Слишком большая правка", (await Assert.ThrowsAsync<AgentToolException>(() => Invoke(EditingAgentTools.ApplyEditsName,
            Edits(("src/A.cs", "void Run()", new string('x', EditPlanner.MaxEditCharacters)))))).Message, StringComparison.Ordinal);

    [Fact]
    public async Task ApplyEdits_WrongIndent_AppliesWithWarning()
    {
        var result = await Invoke(EditingAgentTools.ApplyEditsName, Edits(("src/A.cs", "  void Run() { }\n  void Stop() { }", "  void Go() { }\n  void Stop() { }")));

        Assert.Equal("class A\r\n{\r\n    void Go() { }\r\n    void Stop() { }\r\n}\r\n", Tab("A.cs").Document.Buffer.GetText());
        Assert.Contains("без учёта отступов (строки 3–4)", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_FromModelJson_ShowsOldAndNewText_WithoutChanging()
    {
        var json = JsonDocument.Parse("""[{ "path": "src/B.cs", "oldText": ".Run()", "newText": ".Execute()" }]""").RootElement;

        var previews = await _tools.PreviewAsync(EditingAgentTools.ApplyEditsName, new Dictionary<string, object?> { ["edits"] = json }, TestContext.Current.CancellationToken);

        var preview = Assert.Single(previews);
        Assert.Equal((ProposedChangeKind.Edit, "src/B.cs"), (preview.Kind, preview.RelativePath));
        Assert.Equal("class B { void Go() => new A().Execute(); }\n", preview.NewText);
        Assert.Empty(_fixture.Editors.Tabs);
        Assert.True(_tools.CanPreview(EditingAgentTools.CreateFileName));
        Assert.False(_tools.CanPreview("read_file"));
    }

    // One top-level path, as in Claude Code's MultiEdit (several models send it this way).
    [Fact]
    public async Task ApplyEdits_TopLevelPath_AppliesToEditsWithoutTheirOwn()
    {
        var arguments = new Dictionary<string, object?>
        {
            ["path"] = "src/B.cs",
            ["edits"] = JsonDocument.Parse("""[{"oldText": ".Run()", "newText": ".Execute()"}]""").RootElement,
        };

        await Invoke(EditingAgentTools.ApplyEditsName, arguments);

        Assert.Equal("class B { void Go() => new A().Execute(); }\n", Tab("B.cs").Document.Buffer.GetText());
    }

    [Fact]
    public async Task ApplyEdits_NoPathAnywhere_ExplainsWhatToPass()
    {
        var arguments = new Dictionary<string, object?> { ["edits"] = JsonDocument.Parse("""[{"oldText": ".Run()", "newText": ".Execute()"}]""").RootElement };

        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(EditingAgentTools.ApplyEditsName, arguments));

        Assert.Contains("укажите «path»", error.Message, StringComparison.Ordinal);
    }

    // Some models send the edits array as a JSON string; the card preview parses it as a value.
    [Fact]
    public async Task Preview_EditsAsJsonString_IsParsed()
    {
        var arguments = new Dictionary<string, object?> { ["edits"] = """[{"path": "src/B.cs", "oldText": ".Run()", "newText": ".Execute()"}]""" };

        var preview = Assert.Single(await _tools.PreviewAsync(EditingAgentTools.ApplyEditsName, arguments, TestContext.Current.CancellationToken));

        Assert.Equal("src/B.cs", preview.RelativePath);
    }

    private static Dictionary<string, object?> Edits(params (string Path, string OldText, string NewText)[] edits) =>
        new() { ["edits"] = edits.Select(edit => new FileEdit(edit.Path, edit.OldText, edit.NewText)).ToArray() };

    private EditorTabViewModel Tab(string title) => _fixture.Editors.Tabs.OfType<EditorTabViewModel>().Single(tab => tab.Title == title);

    // The agent edits only files it has read, so edit tests start from the disk text marked as read.
    private void MarkRead(string relative)
    {
        var path = EditorFixture.PathOf(relative);
        _fileState.RecordRead(path, _fixture.FileSystem.ReadAllText(path), 1, 1);
    }

    private async Task<string?> Invoke(string tool, Dictionary<string, object?> arguments) =>
        (await _functions[tool].InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString();
}
