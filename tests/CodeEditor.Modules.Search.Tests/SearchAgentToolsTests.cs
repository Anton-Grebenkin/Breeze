using CodeEditor.Modules.Search.Services;
using CodeEditor.Modules.Search.Services.Agent;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Search.Tests;

public sealed class SearchAgentToolsTests : IDisposable
{
    private const string BothFiles = "src/Program.cs (1)\nsrc/Utils/Helper.cs (1)\n";

    private readonly SearchFixture _fixture = new();
    private readonly AIFunction _tool;

    public SearchAgentToolsTests() =>
        _tool = new SearchAgentTools(_fixture.Search, _fixture.Index, _fixture.Workspace, _fixture.Documents, _fixture.FileSystem, new InlineUiDispatcher()).CreateTools().OfType<AIFunction>().Single();

    public void Dispose() => _fixture.Dispose();

    // A glob with a non-existent folder used to say "nothing found", so the model assumed the text was absent.
    [Fact]
    public async Task IncludeMatchingNoFiles_SaysNothingWasSearched_WithSimilarPaths()
    {
        var result = await Invoke(new() { ["query"] = "Run", ["include"] = "**/Util/**/*.cs" });

        Assert.StartsWith("(под маску «**/Util/**/*.cs» не попал ни один файл — поиск не выполнялся", result, StringComparison.Ordinal);
        Assert.Contains("Похожие пути в папке: src/Utils.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Path_LimitsSearchToFolderOrFile()
    {
        Assert.Equal("src/Utils/Helper.cs (1)\n", await Invoke(new() { ["query"] = "Run", ["path"] = "src/Utils", ["matchCase"] = true, ["mode"] = "files" }));
        Assert.Equal("src/Program.cs (1)\n", await Invoke(new() { ["query"] = "Run", ["path"] = "src/Program.cs", ["matchCase"] = true, ["mode"] = "files" }));
        Assert.Equal(BothFiles, await Invoke(new() { ["query"] = "Run", ["path"] = "src", ["include"] = "*.cs", ["matchCase"] = true, ["mode"] = "files" }));
    }

    [Fact]
    public async Task MissingPath_IsAnErrorWithSimilarPaths()
    {
        var error = await Assert.ThrowsAsync<CodeEditor.Modules.Agent.Contracts.AgentToolException>(() => Invoke(new() { ["query"] = "Run", ["path"] = "Utils" }));

        Assert.Equal("«Utils» — не файл и не папка рабочей папки. Похожие пути в папке: src/Utils.", error.Message);
    }

    [Fact]
    public async Task SearchText_GroupsByFile_InPathOrder()
    {
        var result = await Invoke(new() { ["query"] = "Run", ["include"] = "*.cs", ["matchCase"] = true });

        Assert.Equal("search_text", _tool.Name);
        Assert.Equal("src/Program.cs\n3: static void Main() => Helper.Run();\nsrc/Utils/Helper.cs\n3: public static void Run() { }\n", result);
    }

    [Fact]
    public async Task SearchText_WithContextLines_MarksNeighbours()
    {
        var result = await Invoke(new() { ["query"] = "public static void Run", ["contextLines"] = 1 });

        Assert.Equal("src/Utils/Helper.cs\n2- {\n3: public static void Run() { }\n4- }\n", result);
    }

    [Fact]
    public async Task SearchText_FilesAndCountModes()
    {
        Assert.Equal("src/Program.cs (1)\nsrc/Utils/Helper.cs (1)\n", await Invoke(new() { ["query"] = "Run", ["include"] = "*.cs", ["matchCase"] = true, ["mode"] = "files" }));
        Assert.StartsWith("Совпадений: 2 в 2 файлах.", await Invoke(new() { ["query"] = "Run", ["include"] = "*.cs", ["matchCase"] = true, ["mode"] = "count" }), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchText_Pages_WithOffset()
    {
        var first = await Invoke(new() { ["query"] = "Run", ["include"] = "*.cs", ["matchCase"] = true, ["maxResults"] = 1 });
        var second = await Invoke(new() { ["query"] = "Run", ["include"] = "*.cs", ["matchCase"] = true, ["maxResults"] = 1, ["offset"] = 1 });

        Assert.Equal("src/Program.cs\n3: static void Main() => Helper.Run();\n…(показано 1–1 из 2 совпадений; продолжение — offset=1)", first);
        Assert.Equal("src/Utils/Helper.cs\n3: public static void Run() { }\n", second);
    }

    [Fact]
    public async Task SearchText_SkipsSecretFiles()
    {
        _fixture.FileSystem.AddFile(SearchFixture.PathOf(".env"), "API_KEY=Run");
        _fixture.Workspace.Open(SearchFixture.Root);
        await _fixture.Index.WhenReady;
        Assert.Contains(await _fixture.SearchAsync(new TextSearchOptions("API_KEY")), file => file.RelativePath == ".env");

        Assert.DoesNotContain(".env", await Invoke(new() { ["query"] = "API_KEY" }), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchText_IncludesUnsavedText()
    {
        var document = await _fixture.Documents.OpenAsync(SearchFixture.PathOf("docs/readme.md"), TestContext.Current.CancellationToken);
        document.Buffer.Replace(0, 0, "уникальная_правка\n");

        Assert.Equal("docs/readme.md\n1: уникальная_правка\n", await Invoke(new() { ["query"] = "уникальная_правка" }));
    }

    // The query is a regex, as in ripgrep: an alternation finds both names in one search.
    [Fact]
    public async Task SearchText_IsRegexByDefault() =>
        Assert.Equal(BothFiles, await Invoke(new() { ["query"] = @"Main\(|void Run", ["mode"] = "files" }));

    // A literal with a parenthesis is not an error: it is searched as text, with a note for the model.
    [Fact]
    public async Task SearchText_InvalidRegex_FallsBackToLiteral()
    {
        var result = await Invoke(new() { ["query"] = "Helper.Run(" });

        Assert.StartsWith("(запрос — не регулярное выражение", result, StringComparison.Ordinal);
        Assert.EndsWith("src/Program.cs\n3: static void Main() => Helper.Run();\n", result, StringComparison.Ordinal);
    }

    // An empty result is retried in the other mode with a note (as Copilot does); the requested mode goes first.
    [Fact]
    public async Task SearchText_LiteralWithoutMatches_IsRetriedAsRegex()
    {
        var result = await Invoke(new() { ["query"] = "Main|Run", ["isRegex"] = false, ["matchCase"] = true, ["include"] = "*.cs" });

        Assert.Equal(
            "(как обычный текст ничего не нашлось — ниже совпадения запроса как регулярного выражения)\n" +
            "src/Program.cs\n3: static void Main() => Helper.Run();\nsrc/Utils/Helper.cs\n3: public static void Run() { }\n",
            result);
    }

    [Fact]
    public async Task SearchText_RegexWithoutMatches_IsRetriedAsLiteral() =>
        Assert.Equal(
            "(по регулярному выражению ничего не нашлось — ниже совпадения запроса как обычного текста)\nsrc/Utils/Helper.cs\n3: public static void Run() { }\n",
            await Invoke(new() { ["query"] = "Run() { }" }));

    [Fact]
    public async Task SearchText_NothingInEitherMode_SaysNothingFound() =>
        Assert.Equal("(ничего не найдено)", await Invoke(new() { ["query"] = "Nope|Never", ["isRegex"] = false }));

    [Fact]
    public async Task SearchText_IncludeWithBraces() =>
        Assert.Equal(BothFiles, await Invoke(new() { ["query"] = "Run", ["include"] = "**/*.{cs,sql}", ["matchCase"] = true, ["mode"] = "files" }));

    // A page is also capped by characters, so long lines don't bloat the context (ADR 0012).
    [Fact]
    public async Task SearchText_PageIsLimitedByCharacters()
    {
        var line = "needle " + new string('x', 190);
        _fixture.FileSystem.AddFile(SearchFixture.PathOf("big.txt"), string.Join('\n', Enumerable.Repeat(line, 300)));
        _fixture.Workspace.Open(SearchFixture.Root);
        await _fixture.Index.WhenReady;

        var page = (await Invoke(new() { ["query"] = "needle", ["maxResults"] = 200 }))!;

        var shown = page.Split('\n').Count(text => text.Contains("needle", StringComparison.Ordinal));
        Assert.InRange(shown, 50, 199);
        Assert.InRange(page.Length, SearchOutput.MaxCharacters, SearchOutput.MaxCharacters + 1_000);
        Assert.Contains($"продолжение — offset={shown}", page, StringComparison.Ordinal);
    }

    // Agent chats and memory are skipped; saved output only when the model searches it explicitly.
    [Fact]
    public async Task SearchText_SkipsAgentData_UnlessSearchingSavedOutput()
    {
        _fixture.FileSystem
            .AddFile(SearchFixture.PathOf(".breeze/agent/chat-1.json"), "{\"text\":\"маркер\"}")
            .AddFile(SearchFixture.PathOf(".breeze/agent/outputs/20260928-120000-000-1-run_command.txt"), "error маркер");
        _fixture.Workspace.Open(SearchFixture.Root);
        await _fixture.Index.WhenReady;

        Assert.Equal("(ничего не найдено)", await Invoke(new() { ["query"] = "маркер" }));
        Assert.Equal(
            ".breeze/agent/outputs/20260928-120000-000-1-run_command.txt\n1: error маркер\n",
            await Invoke(new() { ["query"] = "маркер", ["include"] = ".breeze/agent/outputs/**" }));
    }

    private async Task<string?> Invoke(Dictionary<string, object?> arguments) =>
        (await _tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString();
}
