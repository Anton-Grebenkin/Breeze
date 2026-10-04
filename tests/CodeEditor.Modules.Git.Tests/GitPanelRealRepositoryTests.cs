using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Git.Tests;

/// <summary>
/// The panel on real git in a temporary repository: status with Cyrillic and spaces in paths, staging, diff with line
/// numbers, commit from the message box, history and a new branch. Skipped when git is not installed.
/// </summary>
public sealed class GitPanelRealRepositoryTests : IDisposable
{
    private const string Tracked = "Заказ с пробелом.cs";
    private const string Untracked = "новый файл.txt";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "CodeEditor.GitPanelTests", Guid.NewGuid().ToString("N"));
    private readonly ProcessRunner _runner = new();
    private readonly ContextKeyService _context = new();
    private readonly Workspace _workspace;
    private readonly GitRepository _repository;
    private readonly GitReader _reader;
    private readonly GitActions _actions;

    public GitPanelRealRepositoryTests()
    {
        Directory.CreateDirectory(_root);
        var fileSystem = new PhysicalFileSystem();
        _workspace = new Workspace(fileSystem, _context, NullLogger<Workspace>.Instance);
        _workspace.Open(_root);
        var cli = new GitCli(new GitRunner(_runner, _workspace));
        _reader = new GitReader(cli);
        _repository = new GitRepository(_reader, _workspace, _context, new StatusBarViewModel());
        _actions = new GitActions(_repository, cli, fileSystem);
    }

    public void Dispose()
    {
        _repository.Dispose();
        _workspace.Dispose();
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task StatusStageDiffCommitHistoryAndBranch_WorkOnRealGit()
    {
        await InitAsync();
        await File.WriteAllTextAsync(Path.Combine(_root, Tracked), "class Order\n{\n}\n", TestContext.Current.CancellationToken);
        await GitAsync("add", "--", Tracked);
        await GitAsync("commit", "-m", "Первый");
        await File.WriteAllTextAsync(Path.Combine(_root, Tracked), "class Order\n{\n    int Total;\n}\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_root, Untracked), "текст\n", TestContext.Current.CancellationToken);

        await _repository.RefreshAsync();

        Assert.Equal(GitRepositoryState.Ready, _repository.State);
        Assert.Equal("main", _repository.Status.Head.Branch);
        Assert.Equal(
        [
            new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, Tracked),
            new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Untracked, Untracked),
        ], _repository.Status.Changes.OrderBy(change => change.Status));

        var root = _repository.Location!.Root;
        var diff = await _reader.DiffAsync(root, _repository.Status.Changes.First(change => change.Path == Tracked), TestContext.Current.CancellationToken);
        Assert.Contains(new GitDiffRow(GitDiffRowKind.Added, "    int Total;", NewLine: 3), diff);
        Assert.Equal(GitDiffRowKind.Hunk, diff[0].Kind);
        var newFile = await _reader.DiffAsync(root, _repository.Status.Changes.First(change => change.Path == Untracked), TestContext.Current.CancellationToken);
        Assert.Equal(new GitDiffRow(GitDiffRowKind.Added, "текст", NewLine: 1), newFile[^1]);

        Assert.True(await _actions.StageAsync([Untracked]));
        Assert.Contains(new GitFileChange(GitChangeGroup.Staged, GitFileStatus.Added, Untracked), _repository.Status.Changes);

        using var commit = new GitCommitInputViewModel(_repository, _actions, new FakeDialogs(), _context) { Message = "Второй: «новый файл»" };
        Assert.True(await commit.CommitAsync());
        Assert.Equal([new GitFileChange(GitChangeGroup.Changes, GitFileStatus.Modified, Tracked)], _repository.Status.Changes);

        var log = await _reader.LogAsync(root, skip: 0, TestContext.Current.CancellationToken);
        Assert.Equal(["Второй: «новый файл»", "Первый"], log.Select(entry => entry.Subject));
        Assert.Contains("main", log[0].Refs, StringComparison.Ordinal);
        Assert.Equal([new GitCommitFile(GitFileStatus.Added, Untracked)], await _reader.CommitFilesAsync(root, log[0].Hash, TestContext.Current.CancellationToken));

        Assert.True(await _actions.CreateBranchAsync("feature/вход"));
        Assert.Equal("feature/вход", _repository.Status.Head.Branch);
    }

    // A repository with a commit author; skips the test when git is missing.
    private async Task InitAsync()
    {
        try
        {
            await GitAsync("init", "--initial-branch=main");
        }
        catch (InvalidOperationException)
        {
            Assert.Skip("git is not installed.");
        }

        await GitAsync("config", "user.name", "Тест");
        await GitAsync("config", "user.email", "test@example.com");
        await GitAsync("config", "core.autocrlf", "false");
    }

    private Task<ProcessResult> GitAsync(params string[] arguments) =>
        _runner.RunAsync(new ProcessRequest("git", arguments, _root), null, TestContext.Current.CancellationToken);
}
