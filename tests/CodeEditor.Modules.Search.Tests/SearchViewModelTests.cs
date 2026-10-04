using CodeEditor.Modules.Search.ViewModels;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Search.Tests;

public sealed class SearchViewModelTests : IDisposable
{
    private readonly SearchFixture _fixture = new();

    private SearchViewModel Search => _fixture.ViewModel;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Search_GroupsByFileInPathOrder_WithSummary()
    {
        Search.Query = "helper";

        await Search.SearchAsync();

        Assert.Equal(["docs/readme.md", "src/Program.cs", "src/Utils/Helper.cs"], Search.Files.Select(file => file.RelativePath));
        Assert.Equal("3 результата в 3 файлах", Search.Summary);
        Assert.False(Search.IsSearching);

        var program = Search.Files[1];
        Assert.Equal(("Program.cs", "src"), (program.Name, program.Directory));
        var match = Assert.Single(program.Matches);
        Assert.Equal(("static void Main() => ", "Helper", ".Run();"), (match.Before, match.Text, match.After));
    }

    [Fact]
    public async Task NoMatches_SaysSo()
    {
        Search.Query = "отсутствует";

        await Search.SearchAsync();

        Assert.Empty(Search.Files);
        Assert.StartsWith("Ничего не найдено", Search.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRegex_ShowsErrorAndClearsResults()
    {
        Search.Query = "Run";
        await Search.SearchAsync();

        Search.UseRegex = true;
        Search.Query = "Run(";
        await Search.SearchAsync();

        Assert.Empty(Search.Files);
        Assert.StartsWith("Неверное регулярное выражение", Search.Error, StringComparison.Ordinal);

        Search.Query = "Run\\(";
        await Search.SearchAsync();

        Assert.Null(Search.Error);
        Assert.Equal(2, Search.Files.Count);
    }

    [Fact]
    public async Task Clear_EmptiesQueryAndResults()
    {
        Search.Query = "Run";
        await Search.SearchAsync();

        Search.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, Search.Query);
        Assert.Empty(Search.Files);
        Assert.Equal(string.Empty, Search.Summary);
    }

    [Fact]
    public async Task PreviewMatch_OpensPreviewTab_AndKeepsFocus()
    {
        Search.Query = "Run";
        await Search.SearchAsync();
        var match = Search.Files.Single(file => file.Name == "Helper.cs").Matches[0];

        await Search.PreviewMatchCommand.ExecuteAsync(match);

        Assert.Equal((ShellCommandIds.OpenFile, (object?)new OpenFileRequest(SearchFixture.PathOf("src/Utils/Helper.cs"), Preview: true)), _fixture.Executed[0]);
        Assert.Equal(new EditorLocation(3, 24, 3) { PreserveFocus = true }, _fixture.Executed[1].Argument);
    }

    [Fact]
    public async Task OpenMatch_PinsTab_AndFocusesEditor()
    {
        Search.Query = "Run";
        await Search.SearchAsync();

        await Search.OpenMatchCommand.ExecuteAsync(Search.Files[0].Matches[0]);

        Assert.False(((OpenFileRequest)_fixture.Executed[0].Argument!).Preview);
        Assert.False(((EditorLocation)_fixture.Executed[1].Argument!).PreserveFocus);
    }

    [Fact]
    public async Task ToggleCollapseAll_CollapsesThenExpands()
    {
        Search.Query = "Run";
        await Search.SearchAsync();

        Search.ToggleCollapseAllCommand.Execute(null);
        Assert.All(Search.Files, file => Assert.False(file.IsExpanded));

        Search.ToggleCollapseAllCommand.Execute(null);
        Assert.All(Search.Files, file => Assert.True(file.IsExpanded));
    }

    [Fact]
    public void SetFocused_SetsContextKey()
    {
        Search.SetFocused(true);

        Assert.Equal(true, _fixture.Context.GetValue(SearchViewModel.FocusContextKey));
    }

    [Fact]
    public async Task ClosingFolder_ClearsResults()
    {
        Search.Query = "Run";
        await Search.SearchAsync();

        _fixture.Workspace.Close();

        Assert.False(Search.HasWorkspace);
        Assert.Empty(Search.Files);
    }
}
