using CodeEditor.Core.Storage;
using CodeEditor.Shell.Instances;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Instances;

public sealed class WindowRegistryTests
{
    private static readonly DateTime Started = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Noon = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeProcesses _processes = new();
    private readonly WindowRegistry _registry;

    public WindowRegistryTests() =>
        _registry = new WindowRegistry(_fileSystem, new UserDataPaths(@"C:\data"), _processes, NullLogger<WindowRegistry>.Instance);

    [Fact]
    public void Others_AreLiveWindowsExceptSelf_MostRecentFirst()
    {
        Publish(1, @"C:\a", Noon);
        Publish(2, @"C:\b", Noon.AddMinutes(5));
        Publish(3, null, Noon.AddMinutes(1));

        var others = _registry.Others(currentProcessId: 3);

        Assert.Equal([2, 1], others.Select(window => window.ProcessId));
        Assert.Equal(@"C:\b", others[0].Folder);
    }

    [Fact]
    public void ExitedProcess_IsDroppedWithItsFile()
    {
        Publish(1, @"C:\a", Noon);
        _processes.Running.Remove(1);

        Assert.Empty(_registry.Others(currentProcessId: 9));
        Assert.False(_fileSystem.FileExists(EntryFile(1)));
    }

    // Windows reuses process ids: another process with the same id is not the window.
    [Fact]
    public void ReusedProcessId_IsNotTheWindow()
    {
        Publish(1, @"C:\a", Noon);
        _processes.Running[1] = Started.AddHours(1);

        Assert.Empty(_registry.Others(currentProcessId: 9));
    }

    [Fact]
    public void Remove_DeletesTheEntry()
    {
        Publish(1, @"C:\a", Noon);

        _registry.Remove(1);

        Assert.Empty(_registry.Others(currentProcessId: 9));
    }

    [Fact]
    public void CorruptEntry_IsSkipped()
    {
        Publish(1, @"C:\a", Noon);
        _fileSystem.AddFile(EntryFile(2), "{ not json");

        Assert.Equal([1], _registry.Others(currentProcessId: 9).Select(window => window.ProcessId));
    }

    private void Publish(int processId, string? folder, DateTimeOffset lastActive)
    {
        _processes.Running[processId] = Started;
        _registry.Publish(new WindowEntry(processId, Started, folder, lastActive));
    }

    private static string EntryFile(int processId) => Path.Combine(@"C:\data", WindowRegistry.FolderName, $"{processId}.json");

    private sealed class FakeProcesses : IProcessProbe
    {
        public Dictionary<int, DateTime> Running { get; } = [];

        public DateTime? StartTimeOf(int processId) => Running.TryGetValue(processId, out var started) ? started : null;
    }
}
