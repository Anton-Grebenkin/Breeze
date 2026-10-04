using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Settings;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Core.Tests.Settings;

public sealed class ExcludeSettingsTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(Root);
    private readonly Workspace _workspace;
    private readonly TestOptionsMonitor<FilesOptions> _options = new(new FilesOptions());
    private readonly ExcludeSettings _settings;
    private int _rescans;

    public ExcludeSettingsTests()
    {
        _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(Root);
        _workspace.FilesChanged += (_, e) => _rescans += e.RequiresRescan ? 1 : 0;
        _settings = new ExcludeSettings(_workspace, _options);
        _settings.Start();
    }

    public void Dispose()
    {
        _settings.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public void FilesExclude_HidesMatchingPaths()
    {
        _options.Set(new FilesOptions { Exclude = new() { ["**/*.tmp"] = true, ["logs/"] = true, ["keep.tmp"] = false } });

        Assert.True(_workspace.IsExcluded(Path.Combine(Root, "src", "a.tmp"), isDirectory: false));
        Assert.True(_workspace.IsExcluded(Path.Combine(Root, "logs"), isDirectory: true));
        Assert.True(_workspace.IsExcluded(Path.Combine(Root, "logs", "today.txt"), isDirectory: false));
        Assert.False(_workspace.IsExcluded(Path.Combine(Root, "src", "a.cs"), isDirectory: false));
        Assert.Equal(1, _rescans);
    }

    [Fact]
    public void OtherSettingsChanges_DoNotRescan()
    {
        _options.Set(new FilesOptions { Exclude = new() { ["*.tmp"] = true } });

        _options.Set(new FilesOptions { Exclude = new() { ["*.tmp"] = true }, AutoSave = FilesOptions.AutoSaveAfterDelay });

        Assert.Equal(1, _rescans);
    }

    [Fact]
    public void RemovingPattern_ShowsPathAgain()
    {
        _options.Set(new FilesOptions { Exclude = new() { ["*.tmp"] = true } });

        _options.Set(new FilesOptions());

        Assert.False(_workspace.IsExcluded(Path.Combine(Root, "a.tmp"), isDirectory: false));
        Assert.Equal(2, _rescans);
    }
}
