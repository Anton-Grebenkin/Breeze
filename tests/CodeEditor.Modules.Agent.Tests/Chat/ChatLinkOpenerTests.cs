using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Services.Chat;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Chat;

/// <summary>Links in replies: models often name a file without its folder, as VS Code terminal links do.</summary>
public sealed class ChatLinkOpenerTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddFile(PathOf(@"src\Worker\Program.cs"), "class Program {}")
        .AddFile(PathOf(@"src\Api\Orders\Order.cs"), "class Order {}")
        .AddFile(PathOf(@"src\MyApp\Startup.cs"), "class A {}")
        .AddFile(PathOf(@"src\App\Startup.cs"), "class B {}")
        .AddFile(PathOf(@"tests\App\Startup.cs"), "class C {}")
        .AddFile(PathOf("README.md"), "# Readme");

    private readonly Workspace _workspace;
    private readonly FileIndex _index;
    private readonly FakeQuickPick _quickPick = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly List<object?> _opened = [];
    private readonly ChatLinkOpener _links;

    public ChatLinkOpenerTests()
    {
        _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _index = new FileIndex(_workspace, _fileSystem, NullLogger<FileIndex>.Instance);
        _workspace.Open(Root);

        var registry = new CommandRegistry();
        registry.Register(new CommandDefinition(ShellCommandIds.OpenFile, "Открыть", Record));
        registry.Register(new CommandDefinition(ShellCommandIds.EditorGoToLine, "Строка", Record));
        var commands = new CommandService(registry, new ContextKeyService(), NullLogger<CommandService>.Instance);
        _links = new ChatLinkOpener(_workspace, _fileSystem, _index, _quickPick, commands, new FakeSystemShell(), _statusBar);
    }

    public void Dispose()
    {
        _index.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task PathFromRoot_Opens()
    {
        await _links.OpenAsync("README.md");

        Assert.Equal([new OpenFileRequest(PathOf("README.md"))], _opened);
    }

    [Fact]
    public async Task FileNameOnly_FoundInSubfolder_OpensAtLine()
    {
        await _links.OpenAsync("Program.cs:19");

        Assert.Equal([new OpenFileRequest(PathOf(@"src\Worker\Program.cs")), new EditorLocation(19, 1)], _opened);
    }

    [Fact]
    public async Task PartialPath_MatchesWholeFolderNames()
    {
        await _links.OpenAsync("Orders/Order.cs");

        Assert.Equal([new OpenFileRequest(PathOf(@"src\Api\Orders\Order.cs"))], _opened);
    }

    // "App/Startup.cs" is not inside "MyApp": a match ends at a folder boundary.
    [Fact]
    public async Task SeveralMatches_OfferedShortestFirst_PickedOneOpens()
    {
        await _links.OpenAsync("App/Startup.cs");

        Assert.Empty(_opened);
        Assert.Equal(["src/App/Startup.cs", "tests/App/Startup.cs"], _quickPick.Items.Select(item => item.Title));

        await _quickPick.PickAsync("tests");
        Assert.Equal([new OpenFileRequest(PathOf(@"tests\App\Startup.cs"))], _opened);
    }

    [Fact]
    public async Task UnknownFile_ReportedInStatusBar()
    {
        await _links.OpenAsync("Missing.cs");

        Assert.Empty(_opened);
        Assert.Equal("Файл не найден: Missing.cs", _statusBar.Message);
    }

    private static string PathOf(string relative) => Path.Combine(Root, relative);

    private ValueTask Record(object? argument, CancellationToken cancellationToken)
    {
        _opened.Add(argument);
        return ValueTask.CompletedTask;
    }
}
