using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Tests.Editors;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Palette;

public sealed class FilesQuickOpenProviderTests : IDisposable
{
    private readonly EditorAreaFixture _fixture = new();
    private readonly FileIndex _index;
    private readonly RecentFiles _recent;
    private readonly List<(string Id, object? Argument)> _executed = [];
    private readonly FilesQuickOpenProvider _provider;
    private readonly GoToLineQuickOpenProvider _goToLine;

    public FilesQuickOpenProviderTests()
    {
        _fixture.FileSystem
            .AddFile(EditorAreaFixture.PathOf(Path.Combine("src", "Program.cs")))
            .AddFile(EditorAreaFixture.PathOf(Path.Combine("src", "Utils", "ProgressBar.cs")));
        _index = new FileIndex(_fixture.Workspace, _fixture.FileSystem, NullLogger<FileIndex>.Instance);
        _recent = new RecentFiles(_fixture.Area);

        var commands = new CommandRegistry();
        foreach (var id in new[] { ShellCommandIds.OpenFile, ShellCommandIds.EditorGoToLine, ShellCommandIds.FocusActiveEditor })
        {
            commands.Register(new CommandDefinition(id, id, (argument, _) =>
            {
                _executed.Add((id, argument));
                return ValueTask.CompletedTask;
            }));
        }

        var commandService = new CommandService(commands, _fixture.Context, NullLogger<CommandService>.Instance);
        _provider = new FilesQuickOpenProvider(_index, _fixture.Workspace, _recent, commandService, new InlineUiDispatcher());
        _goToLine = new GoToLineQuickOpenProvider(_fixture.Area, commandService);
    }

    public void Dispose()
    {
        _recent.Dispose();
        _index.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task Filter_PrefersNameMatchAndShowsFolder()
    {
        await PrepareAsync();

        var items = _provider.Filter("prog");

        Assert.Equal(["Program.cs", "ProgressBar.cs"], items.Select(item => item.Title));
        Assert.Equal("src", items[0].Detail);
        Assert.Equal("src/Utils", items[1].Detail);
        Assert.Equal([0, 1, 2, 3], items[0].Highlights);
    }

    [Fact]
    public async Task Filter_MatchesPathWhenNameDoesNot()
    {
        await PrepareAsync();

        var items = _provider.Filter("utilsbar");

        Assert.Equal("ProgressBar.cs", Assert.Single(items).Title);
    }

    [Fact]
    public async Task EmptyQuery_ListsRecentFilesMostRecentFirst()
    {
        await _fixture.OpenAsync("a.cs");
        await _fixture.OpenAsync("b.cs");
        await PrepareAsync();

        var items = _provider.Filter(string.Empty);

        Assert.Equal(["b.cs", "a.cs"], items.Select(item => item.Title));
        Assert.All(items, item => Assert.True(item.IsRecent));
    }

    [Fact]
    public async Task RecentFile_RanksAboveEqualMatch()
    {
        await _fixture.OpenAsync("c.cs");
        await PrepareAsync();

        var items = _provider.Filter(".cs");

        Assert.Equal("c.cs", items[0].Title);
    }

    [Theory]
    [InlineData("Program.cs:42", "Program.cs", 42)]
    [InlineData("prog:7", "prog", 7)]
    [InlineData("a:b", "a:b", null)]
    [InlineData("file:0", "file:0", null)]
    [InlineData(@"C:\x", @"C:\x", null)]
    public void SplitLine_ParsesTrailingLineNumber(string text, string pattern, int? line) =>
        Assert.Equal((pattern, line), FilesQuickOpenProvider.SplitLine(text));

    [Fact]
    public async Task Accept_WithLine_OpensThenGoesToLine()
    {
        await PrepareAsync();
        var item = _provider.Filter("program:12")[0];

        await _provider.AcceptAsync(item, "program:12");

        Assert.Equal(ShellCommandIds.OpenFile, _executed[0].Id);
        Assert.Equal(new OpenFileRequest(EditorAreaFixture.PathOf(Path.Combine("src", "Program.cs"))), _executed[0].Argument);
        Assert.Equal((ShellCommandIds.EditorGoToLine, (object?)12), _executed[1]);
    }

    [Fact]
    public async Task Accept_WithoutLine_FocusesEditor()
    {
        await PrepareAsync();

        await _provider.AcceptAsync(_provider.Filter("program")[0], "program");

        Assert.Equal([ShellCommandIds.OpenFile, ShellCommandIds.FocusActiveEditor], _executed.Select(entry => entry.Id));
    }

    [Fact]
    public async Task GoToLine_NeedsActiveEditorAndPositiveNumber()
    {
        Assert.Empty(_goToLine.Filter("5"));

        await _fixture.OpenAsync("a.cs");

        Assert.Empty(_goToLine.Filter("abc"));
        Assert.Empty(_goToLine.Filter("0"));
        var item = Assert.Single(_goToLine.Filter(" 5 "));
        Assert.Equal("a.cs", item.Detail);

        await _goToLine.AcceptAsync(item, "5");
        Assert.Equal((ShellCommandIds.EditorGoToLine, (object?)5), Assert.Single(_executed));
    }

    private async Task PrepareAsync()
    {
        // The index was created after the fixture opened the folder, so rebuild it explicitly.
        _fixture.Workspace.Open(EditorAreaFixture.Root);
        await _index.WhenReady;
        _provider.Prepare();
    }
}
