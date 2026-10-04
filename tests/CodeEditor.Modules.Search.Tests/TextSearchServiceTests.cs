using CodeEditor.Modules.Search.Services;

namespace CodeEditor.Modules.Search.Tests;

public sealed class TextSearchServiceTests : IDisposable
{
    private readonly SearchFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Search_FindsInTextFiles_SkipsExcludedAndBinary()
    {
        var results = await _fixture.SearchAsync(new TextSearchOptions("helper"));

        Assert.Equal(["docs/readme.md", "src/Program.cs", "src/Utils/Helper.cs"], results.Select(result => result.RelativePath));
        var program = results[1];
        Assert.Equal(SearchFixture.PathOf("src/Program.cs"), program.FullPath);
        Assert.Equal((3, 27), (program.Matches[0].Line, program.Matches[0].Column));
    }

    [Fact]
    public async Task Include_LimitsToMatchingFiles()
    {
        var results = await _fixture.SearchAsync(new TextSearchOptions("Run") { Include = "*.cs" });

        Assert.Equal(["src/Program.cs", "src/Utils/Helper.cs"], results.Select(result => result.RelativePath));
    }

    [Fact]
    public async Task Exclude_RemovesFolders()
    {
        var results = await _fixture.SearchAsync(new TextSearchOptions("Run") { Exclude = "src/Utils, docs" });

        Assert.Equal("src/Program.cs", Assert.Single(results).RelativePath);
    }

[Fact]    public async Task SearchExcludeSetting_AddsToFieldMasks()    {        _fixture.SearchSettings.Set(new SearchSettings { Exclude = new() { ["docs"] = true, ["src/Utils"] = false } });        var results = await _fixture.SearchAsync(new TextSearchOptions("Run") { Exclude = "src/Program.cs" });        Assert.Equal("src/Utils/Helper.cs", Assert.Single(results).RelativePath);    }
    [Fact]
    public async Task UnsavedText_IsSearchedInsteadOfDisk()
    {
        var unsaved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SearchFixture.PathOf("src/Program.cs")] = "// несохранённая правка\r\nclass Program { }",
        };

        var results = await _fixture.SearchAsync(new TextSearchOptions("несохранённая"), unsaved);

        Assert.Equal("src/Program.cs", Assert.Single(results).RelativePath);
    }

    [Fact]
    public async Task InvalidRegex_ThrowsBeforeEnumeration()
    {
        await _fixture.Index.WhenReady;

        Assert.Throws<ArgumentException>(() =>
            _fixture.Search.SearchAsync(new TextSearchOptions("[") { UseRegex = true }, new Dictionary<string, string>(), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_StopsEnumeration()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _fixture.Search.SearchAsync(new TextSearchOptions("Run"), new Dictionary<string, string>(), cancellation.Token))
            {
            }
        });
    }

    [Fact]
    public async Task EarlyStop_EndsProducer()
    {
        for (var i = 0; i < 200; i++)
        {
            _fixture.FileSystem.AddFile(SearchFixture.PathOf($"gen/File{i}.cs"), "Run();");
        }

        _fixture.Workspace.Open(SearchFixture.Root);

        await foreach (var result in _fixture.Search.SearchAsync(new TextSearchOptions("Run"), new Dictionary<string, string>(), CancellationToken.None))
        {
            Assert.NotEmpty(result.Matches);
            break;
        }
    }
}
