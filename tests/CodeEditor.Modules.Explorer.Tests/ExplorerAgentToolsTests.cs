using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class ExplorerAgentToolsTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Combine(Root, "README.md"), "# Проект\n")
        .AddFile(Path.Combine(Root, "src", "Program.cs"), "line1\r\nline2\r\nline3\r\nline4\r\n")
        .AddFile(Path.Combine(Root, "src", "Utils", "Helper.cs"), "class Helper { }")
        .AddFile(Path.Combine(Root, "bin", "app.dll"), "x")
        .AddBytes(Path.Combine(Root, "logo.png"), [0x89, 0x50, 0x00, 0x00]);

    private readonly Workspace _workspace;
    private readonly FileIndex _index;
    private readonly DocumentService _documents;
    private readonly Dictionary<string, AIFunction> _tools;

    private AgentFileState FileState { get; } = new();

    public ExplorerAgentToolsTests()
    {
        _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _index = new FileIndex(_workspace, _fileSystem, NullLogger<FileIndex>.Instance);
        _workspace.Open(Root);
        _documents = new DocumentService(_fileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), _workspace, NullLogger<DocumentService>.Instance);
        _tools = new ExplorerAgentTools(_workspace, _fileSystem, _index, _documents, new InlineUiDispatcher(), FileState)
            .CreateTools().OfType<AIFunction>().ToDictionary(tool => tool.Name);
    }

    public void Dispose()
    {
        _documents.Dispose();
        _index.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task ListDir_FoldersFirst_HidesExcluded()
    {
        Assert.Equal("src/\nlogo.png\nREADME.md", await Invoke("list_dir", new() { ["path"] = "." }));
        Assert.Equal("Utils/\nProgram.cs", await Invoke("list_dir", new() { ["path"] = "src" }));
    }

    [Fact]
    public async Task ListDir_WithDepth_ShowsTree()
    {
        Assert.Equal("src/\n  Utils/\n    Helper.cs\n  Program.cs\nlogo.png\nREADME.md", await Invoke("list_dir", new() { ["path"] = ".", ["depth"] = 3 }));
        Assert.Equal("src/\n  Utils/\n  Program.cs\nlogo.png\nREADME.md", await Invoke("list_dir", new() { ["path"] = ".", ["depth"] = 2 }));
    }

    [Theory]
    [InlineData(@"..\other")]
    [InlineData(@"C:\Windows")]
    public async Task PathsOutsideWorkspace_AreRejected(string path)
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke("read_file", new() { ["path"] = path }));

        Assert.Contains("вне рабочей папки", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_NumbersLines_AndPages()
    {
        Assert.Equal("1\tline1\n2\tline2\n3\tline3\n4\tline4\n5\t\n", await Invoke("read_file", new() { ["path"] = "src/Program.cs" }));

        var page = await Invoke("read_file", new() { ["path"] = "src/Program.cs", ["startLine"] = 2, ["maxLines"] = 2 });

        Assert.Equal("2\tline2\n3\tline3\n…(строки 2–3 из 5; продолжение — startLine=4)", page);
    }

    [Fact]
    public async Task ReadFile_UsesUnsavedEditorText()
    {
        var document = await _documents.OpenAsync(Path.Combine(Root, "README.md"), TestContext.Current.CancellationToken);
        document.Buffer.Replace(0, 0, "несохранённое ");

        Assert.Equal("1\tнесохранённое # Проект\n2\t\n", await Invoke("read_file", new() { ["path"] = "README.md" }));
    }

    [Fact]
    public async Task ReadFile_BinaryOrMissing_Explains()
    {
        Assert.Contains("двоичный", (await Assert.ThrowsAsync<AgentToolException>(() => Invoke("read_file", new() { ["path"] = "logo.png" }))).Message, StringComparison.Ordinal);
        Assert.Contains("нет", (await Assert.ThrowsAsync<AgentToolException>(() => Invoke("read_file", new() { ["path"] = "nope.cs" }))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_Folder_ListsItsContents() =>
        Assert.Equal("(«src» — папка, а не файл; её содержимое:)\nUtils/\nProgram.cs", await Invoke("read_file", new() { ["path"] = "src" }));

    [Fact]
    public async Task ReadFile_SameWindowTwice_SaysUnchanged_UntilTextChanges()
    {
        await Invoke("read_file", new() { ["path"] = "src/Program.cs" });

        Assert.Equal("(Строки 1–5 файла src/Program.cs не изменились с прошлого чтения в этом чате — используйте прежний результат.)",
            await Invoke("read_file", new() { ["path"] = "src/Program.cs" }));

        var document = await _documents.OpenAsync(Path.Combine(Root, "src", "Program.cs"), TestContext.Current.CancellationToken);
        document.Buffer.Replace(0, 0, "// ");
        Assert.StartsWith("1\t// line1", await Invoke("read_file", new() { ["path"] = "src/Program.cs" }), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFile_Secret_IsRefused_AndEmptyFileIsMarked()
    {
        _fileSystem.AddFile(Path.Combine(Root, ".env"), "TOKEN=123").AddFile(Path.Combine(Root, "empty.txt"), string.Empty);

        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke("read_file", new() { ["path"] = ".env" }));

        Assert.Contains("секретами", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("123", error.Message, StringComparison.Ordinal);
        Assert.Equal("(пустой файл)", await Invoke("read_file", new() { ["path"] = "empty.txt" }));
    }

    [Fact]
    public async Task FindFiles_ByGlobOrPathPart()
    {
        await _index.WhenReady;

        Assert.Equal("src/Program.cs\nsrc/Utils/Helper.cs", await Invoke("find_files", new() { ["pattern"] = "**/*.cs" }));
        Assert.Equal("src/Utils/Helper.cs", await Invoke("find_files", new() { ["pattern"] = "utils" }));
        Assert.Equal("(ничего не найдено)", await Invoke("find_files", new() { ["pattern"] = "*.java" }));
    }

    // A glob or path that misses shows the model the real names (e.g. "**/Catalog/**" for folder "Acme.Catalog").
    [Fact]
    public async Task NotFound_SuggestsSimilarPaths()
    {
        await _index.WhenReady;

        Assert.Equal("(ничего не найдено) Похожие пути в папке: src/Utils.", await Invoke("find_files", new() { ["pattern"] = "**/Util/**/*.cs" }));
        var missing = await Assert.ThrowsAsync<AgentToolException>(() => Invoke("read_file", new() { ["path"] = "Utils/Missing.cs" }));
        Assert.EndsWith("Похожие пути в папке: src/Utils.", missing.Message, StringComparison.Ordinal);
    }

    // A unique file name resolves to its path; an ambiguous one lists the candidates.
    [Fact]
    public async Task ReadFile_ResolvesUniqueFileName()
    {
        await _index.WhenReady;

        var result = await Invoke("read_file", new() { ["path"] = "Helper.cs" });

        Assert.Equal("(«Helper.cs» — это src/Utils/Helper.cs)\n1\tclass Helper { }\n", result);
    }

    [Fact]
    public async Task ReadFile_AmbiguousName_ListsCandidates()
    {
        _fileSystem.AddFile(Path.Combine(Root, "tests", "Helper.cs"), "class TestHelper { }");
        _workspace.Open(Root);
        await _index.WhenReady;

        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke("read_file", new() { ["path"] = "Helper.cs" }));

        Assert.StartsWith("«Helper.cs» подходит к нескольким файлам:", error.Message, StringComparison.Ordinal);
        Assert.Contains("src/Utils/Helper.cs", error.Message, StringComparison.Ordinal);
        Assert.Contains("tests/Helper.cs", error.Message, StringComparison.Ordinal);
    }

    // A typical file is read whole in one page.
    [Fact]
    public void DefaultPage_IsAThousandLines() => Assert.Equal(1000, ExplorerAgentTools.DefaultMaxLines);

    // Long lines end the page before the line limit; the hint names where to continue (ADR 0012).
    [Fact]
    public async Task ReadFile_PageIsLimitedByCharacters()
    {
        var line = new string('x', 100);
        _fileSystem.AddFile(Path.Combine(Root, "big.txt"), string.Join('\n', Enumerable.Repeat(line, 1_000)));

        var page = (await Invoke("read_file", new() { ["path"] = "big.txt" }))!;

        var shown = page.Split('\n').Count(text => text.EndsWith(line, StringComparison.Ordinal));
        Assert.InRange(page.Length, ExplorerAgentTools.MaxCharactersPerRead - 4_000, ExplorerAgentTools.MaxCharactersPerRead + 200);
        Assert.Contains($"продолжение — startLine={shown + 1}", page, StringComparison.Ordinal);
    }

    // Agent data (chat history, memory, outputs) is hidden from the agent's file listings.
    [Fact]
    public async Task AgentData_IsHiddenFromListsAndFind()
    {
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "agent", "chat-1.json"), "{}").AddFile(Path.Combine(Root, ".breeze", "settings.json"), "{}");
        _workspace.Open(Root);
        await _index.WhenReady;

        Assert.Equal("settings.json", await Invoke("list_dir", new() { ["path"] = ".breeze" }));
        Assert.Equal(".breeze/settings.json", await Invoke("find_files", new() { ["pattern"] = ".breeze" }));
    }

    private async Task<string?> Invoke(string tool, Dictionary<string, object?> arguments) =>
        (await _tools[tool].InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString();
}
