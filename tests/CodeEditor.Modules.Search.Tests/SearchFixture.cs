using System.Text;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Search.Services;
using CodeEditor.Modules.Search.Services.Matching;
using CodeEditor.Modules.Search.ViewModels;
using CodeEditor.Shell.Commands;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Search.Tests;

/// <summary>
/// In-memory <c>C:\repo</c> with sources, a binary file and an excluded <c>bin</c>; real index, documents and
/// commands. Open-file and go-to commands are recorded in <see cref="Executed"/>.
/// </summary>
internal sealed class SearchFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    public SearchFixture()
    {
        FileSystem = new FakeFileSystem()
            .AddDirectory(Root)
            .AddFile(PathOf("src/Program.cs"), "class Program\r\n{\r\n    static void Main() => Helper.Run();\r\n}\r\n")
            .AddFile(PathOf("src/Utils/Helper.cs"), "static class Helper\r\n{\r\n    public static void Run() { }\r\n}\r\n")
            .AddFile(PathOf("docs/readme.md"), "# Helper\nЗапуск: Run\n")
            .AddFile(PathOf("bin/Debug/Program.cs"), "class Program { Helper }")
            .AddBytes(PathOf("app.dll"), [.. Encoding.ASCII.GetBytes("Helper"), 0, 0, 1]);

        Workspace = new Core.Files.Workspace(FileSystem, Context, NullLogger<Core.Files.Workspace>.Instance);
        Index = new FileIndex(Workspace, FileSystem, NullLogger<FileIndex>.Instance);
        Workspace.Open(Root);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), Workspace, NullLogger<DocumentService>.Instance);
        Search = new TextSearchService(Index, FileSystem, SearchSettings, NullLogger<TextSearchService>.Instance);

        var commands = new CommandRegistry();
        foreach (var id in new[] { ShellCommandIds.OpenFile, ShellCommandIds.EditorGoToLine })
        {
            commands.Register(new CommandDefinition(id, id, (argument, _) =>
            {
                Executed.Add((id, argument));
                return ValueTask.CompletedTask;
            }));
        }

        ViewModel = new SearchViewModel(
            Search, Documents, new CommandService(commands, Context, NullLogger<CommandService>.Instance), Workspace, TimeProvider.System, Context);
    }

    public FakeFileSystem FileSystem { get; }

    public ContextKeyService Context { get; } = new();

    public Core.Files.Workspace Workspace { get; }

    public FileIndex Index { get; }

    public DocumentService Documents { get; }

    public TextSearchService Search { get; }

    public TestOptionsMonitor<SearchSettings> SearchSettings { get; } = new(new SearchSettings());

    public SearchViewModel ViewModel { get; }

    public List<(string Id, object? Argument)> Executed { get; } = [];

    public static string PathOf(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public async Task<List<FileSearchResult>> SearchAsync(TextSearchOptions options, IReadOnlyDictionary<string, string>? unsaved = null)
    {
        var results = new List<FileSearchResult>();
        await foreach (var result in Search.SearchAsync(options, unsaved ?? new Dictionary<string, string>(), CancellationToken.None))
        {
            results.Add(result);
        }

        return [.. results.OrderBy(result => result.RelativePath, StringComparer.Ordinal)];
    }

    public void Dispose()
    {
        ViewModel.Dispose();
        Documents.Dispose();
        Index.Dispose();
        Workspace.Dispose();
    }
}
