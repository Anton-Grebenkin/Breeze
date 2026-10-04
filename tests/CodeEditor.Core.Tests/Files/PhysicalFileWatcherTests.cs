using System.Collections.Concurrent;
using CodeEditor.Core.Files;

namespace CodeEditor.Core.Tests.Files;

/// <summary>
/// Watching a real temp folder on disk: events arrive in batches and excluded paths are filtered out.
/// </summary>
public sealed class PhysicalFileWatcherTests : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task CreatedFile_IsReportedInBatch()
    {
        var batches = new BlockingCollection<FileChangesEventArgs>();
        using var watcher = new PhysicalFileSystem().Watch(_root, _ => false);
        watcher.Changed += (_, e) => batches.Add(e);

        await File.WriteAllTextAsync(Path.Combine(_root, "new.cs"), "class A {}", TestContext.Current.CancellationToken);

        Assert.True(batches.TryTake(out var batch, EventTimeout));
        Assert.Contains(batch.Changes, change => change.Path.EndsWith("new.cs", StringComparison.Ordinal) && change.Kind == FileChangeKind.Created);
    }

    [Fact]
    public async Task ExcludedPaths_AreNotReported()
    {
        var batches = new BlockingCollection<FileChangesEventArgs>();
        using var watcher = new PhysicalFileSystem().Watch(_root, path => path.EndsWith(".log", StringComparison.Ordinal));
        watcher.Changed += (_, e) => batches.Add(e);

        await File.WriteAllTextAsync(Path.Combine(_root, "debug.log"), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_root, "marker.txt"), "x", TestContext.Current.CancellationToken);

        Assert.True(batches.TryTake(out var batch, EventTimeout));
        Assert.DoesNotContain(batch.Changes, change => change.Path.EndsWith(".log", StringComparison.Ordinal));
    }

    [Fact]
    public void EnumerateEntries_ListsImmediateChildren()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src", "deep"));
        File.WriteAllText(Path.Combine(_root, "readme.md"), "#");

        var entries = new PhysicalFileSystem().EnumerateEntries(_root).OrderBy(entry => entry.Name).ToArray();

        Assert.Equal(["readme.md", "src"], entries.Select(entry => entry.Name));
        Assert.Equal([false, true], entries.Select(entry => entry.IsDirectory));
    }
}
