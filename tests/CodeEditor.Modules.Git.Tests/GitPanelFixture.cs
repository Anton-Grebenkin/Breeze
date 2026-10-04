using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Git.Commands;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Modules.Git.ViewModels.Tabs;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// The git panel over a fake process runner and an in-memory file system:
/// <code>
/// C:\repo\.git\
/// C:\repo\src\A.cs, C:\repo\new.txt
/// </code>
/// git replies are queued in call order (<see cref="FakeProcessRunner.Returns"/>).
/// </summary>
internal sealed class GitPanelFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    // Options GitRunner puts before every command, and the repository scope added by GitCli.
    private const int RunnerOptions = 5;
    private const int RepositoryScope = 3;
    private const string ReadOnly = "--no-optional-locks";

    public GitPanelFixture()
    {
        FileSystem = new FakeFileSystem()
            .AddDirectory(Path.Combine(Root, ".git"))
            .AddFile(Path.Combine(Root, "src", "A.cs"), "class A {}")
            .AddFile(Path.Combine(Root, "new.txt"), "new");
        Workspace = new Workspace(FileSystem, Context, NullLogger<Workspace>.Instance);
        Workspace.Open(Root);
        CommandService = new CommandService(Commands, Context, NullLogger<CommandService>.Instance);
        Commands.Register(new CommandDefinition("workbench.file.open", "Открыть файл", (argument, _) =>
        {
            OpenedFiles.Add(((OpenFileRequest)argument!).FilePath);
            return ValueTask.CompletedTask;
        }));

        Cli = new GitCli(new GitRunner(Runner, Workspace));
        Reader = new GitReader(Cli);
        Repository = new GitRepository(Reader, Workspace, Context, StatusBar);
        Actions = new GitActions(Repository, Cli, FileSystem);
        Changes = new GitChangesViewModel(Repository, Context);
        Commit = new GitCommitInputViewModel(Repository, Actions, Dialogs, Context);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), Workspace, NullLogger<DocumentService>.Instance);
        Editors = new EditorAreaViewModel(Documents, [new PlainEditorProvider()], [], new DocumentSaver(Documents, Dialogs, StatusBar), Context, StatusBar);
        Diffs = new GitDiffFactory(Repository, Reader, FileSystem, CommandService, Shell);
        Tabs = new GitEditorTabs(new EditorViews(Editors), Diffs, Repository, Reader, Shell);
        Handlers = new GitChangeHandlers(Repository, Actions, Changes, Tabs, Dialogs, CommandService, Editors);
    }

    public FakeProcessRunner Runner { get; } = new();

    public FakeFileSystem FileSystem { get; }

    public ContextKeyService Context { get; } = new();

    public CommandRegistry Commands { get; } = new();

    public CommandService CommandService { get; }

    public Workspace Workspace { get; }

    public StatusBarViewModel StatusBar { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public FakeSystemShell Shell { get; } = new();

    public GitCli Cli { get; }

    public GitReader Reader { get; }

    public GitRepository Repository { get; }

    public GitActions Actions { get; }

    public GitChangesViewModel Changes { get; }

    public GitCommitInputViewModel Commit { get; }

    public DocumentService Documents { get; }

    public EditorAreaViewModel Editors { get; }

    public GitDiffFactory Diffs { get; }

    public GitEditorTabs Tabs { get; }

    public GitChangeHandlers Handlers { get; }

    public List<string> OpenedFiles { get; } = [];

    /// <summary><c>git rev-parse</c> reply: repository root and .git folder, as git prints them.</summary>
    public static string Location => "C:/repo\nC:/repo/.git\n";

    /// <summary><c>git status --porcelain=v2 -z</c> output for branch main with the given records.</summary>
    public static string Status(params string[] records) =>
        string.Join('\0', ["# branch.oid 2db283567e1b42094c8db604b34b7445f2614364", "# branch.head main", .. records]) + "\0";

    public static string Modified(string path, string xy = ".M") =>
        $"1 {xy} N... 100644 100644 100644 83db48f84ec878fbfb30b46d16630e944e34f205 83db48f84ec878fbfb30b46d16630e944e34f205 {path}";

    public static string Untracked(string path) => $"? {path}";

    /// <summary>Locates the repository and reads state, then clears the git request log.</summary>
    public async Task OpenRepositoryAsync(params string[] records)
    {
        Runner.Returns(0, Location).Returns(0, Status(records));
        await Repository.RefreshAsync();
        Runner.Requests.Clear();
    }

    /// <summary>git arguments without common options, repository scope (<c>-C root</c>) and the read-only flag.</summary>
    public IReadOnlyList<string> GitArgs(int index)
    {
        var arguments = Runner.Requests[index].Arguments.Skip(RunnerOptions + RepositoryScope).ToList();
        return arguments is [ReadOnly, .. var rest] ? rest : arguments;
    }

    public IReadOnlyList<IReadOnlyList<string>> AllGitArgs() => [.. Runner.Requests.Select((_, index) => GitArgs(index))];

    public GitChangeItemViewModel Item(GitChangeGroup group, string path) =>
        Changes.Group(group).Items.Single(item => item.Path == path);

    public void Dispose()
    {
        Changes.Dispose();
        Commit.Dispose();
        Repository.Dispose();
        Documents.Dispose();
        Workspace.Dispose();
    }

    private sealed class PlainEditorProvider : IEditorProvider
    {
        public int Priority => 0;

        public bool CanOpen(string filePath) => true;

        public object CreateEditor(IDocument document) => document;
    }
}
