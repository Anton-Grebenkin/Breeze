using System.Text;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using CodeEditor.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeEditor.Core.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");
    private static readonly string Repo = Path.GetFullPath(@"C:\repo");
    private static readonly string UserFile = Path.Combine(UserData, SettingsService.FileName);
    private static readonly string FolderFile = Path.Combine(Repo, SettingsService.WorkspaceFolder, SettingsService.FileName);

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData).AddDirectory(Repo);
    private readonly ServiceProvider _services;

    public SettingsServiceTests()
    {
        _fileSystem.AddFile(UserFile, "// мои настройки\n{\n    \"sample.size\": 16,\n    \"sample.name\": \"user\"\n}\n");

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddSingleton(new UserDataPaths(UserData));
        services.AddSingleton<IContextKeyService, ContextKeyService>();
        services.AddSingleton<IWorkspace, Workspace>();
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSettingsSection<SampleOptions>("sample");
        _services = services.BuildServiceProvider();
    }

    private ISettingsService Settings => _services.GetRequiredService<ISettingsService>();

    private IOptionsMonitor<SampleOptions> Sample => _services.GetRequiredService<IOptionsMonitor<SampleOptions>>();

    private FakeFileWatcher UserWatcher => _fileSystem.Watchers[0];

    public void Dispose() => _services.Dispose();

    [Fact]
    public void UserFile_BindsSection()
    {
        Assert.Equal(16, Sample.CurrentValue.Size);
        Assert.Equal("user", Sample.CurrentValue.Name);
        Assert.True(Sample.CurrentValue.Enabled);
    }

    [Fact]
    public void ChangedOnDisk_UpdatesMonitorAndNotifies()
    {
        _ = Settings;
        SampleOptions? changed = null;
        using var subscription = Sample.OnChange(options => changed = options);

        _fileSystem.AddFile(UserFile, "{ \"sample.size\": 20 }");
        UserWatcher.Raise(new FileChange(UserFile, FileChangeKind.Changed));

        Assert.Equal(20, Sample.CurrentValue.Size);
        Assert.Equal(20, changed?.Size);
        Assert.Equal("default", Sample.CurrentValue.Name);
    }

    [Fact]
    public void FolderSettings_OverrideUser_AndFollowFolder()
    {
        _fileSystem.AddFile(FolderFile, "{ \"sample\": { \"name\": \"folder\" } }");
        var workspace = _services.GetRequiredService<IWorkspace>();
        _ = Settings;

        workspace.Open(Repo);

        Assert.Equal(FolderFile, Settings.WorkspaceSettingsPath);
        Assert.Equal("folder", Sample.CurrentValue.Name);
        Assert.Equal(16, Sample.CurrentValue.Size);

        workspace.Close();

        Assert.Equal("user", Sample.CurrentValue.Name);
    }

    [Fact]
    public void BrokenFile_KeepsLastGoodValues_AndReportsError()
    {
        _ = Settings;
        _fileSystem.AddFile(UserFile, "{ \"sample.size\": ");
        UserWatcher.Raise(new FileChange(UserFile, FileChangeKind.Changed));

        Assert.Equal(16, Sample.CurrentValue.Size);
        Assert.Contains(SettingsService.FileName, Settings.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongType_KeepsDefaults()
    {
        _fileSystem.AddFile(UserFile, "{ \"sample.size\": \"большой\" }");
        _ = Settings;

        Assert.Equal(SampleOptions.DefaultSize, Sample.CurrentValue.Size);
    }

    [Fact]
    public void TrySetUserValue_WritesKeepingComments_AndAppliesImmediately()
    {
        Assert.True(Settings.TrySetUserValue("sample.name", "новое", out var error));

        Assert.Null(error);
        Assert.Equal("новое", Sample.CurrentValue.Name);
        var text = Encoding.UTF8.GetString(_fileSystem.ReadAllBytes(UserFile));
        Assert.StartsWith("// мои настройки", text, StringComparison.Ordinal);
        Assert.Contains("\"sample.name\": \"новое\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySetUserValue_Null_RemovesKey_BackToDefault()
    {
        Assert.True(Settings.TrySetUserValue("sample.name", "новое", out _));

        Assert.True(Settings.TrySetUserValue("sample.name", null, out var error));

        Assert.Null(error);
        Assert.Equal(new SampleOptions().Name, Sample.CurrentValue.Name);
        Assert.DoesNotContain("sample.name", _fileSystem.ReadAllText(UserFile), StringComparison.Ordinal);
    }

    [Fact]
    public void TrySetUserValue_BrokenFile_Refuses()
    {
        _fileSystem.AddFile(UserFile, "{ oops");

        Assert.False(Settings.TrySetUserValue("sample.name", "x", out var error));
        Assert.NotNull(error);
        Assert.Equal("{ oops", _fileSystem.ReadAllText(UserFile));
    }

    [Fact]
    public void EnsureUserSettingsFile_CreatesTemplateOnce()
    {
        var fileSystem = new FakeFileSystem().AddDirectory(UserData);
        using var settings = new SettingsService(
            fileSystem, new UserDataPaths(UserData), new Workspace(fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance),
            new InlineUiDispatcher(), NullLogger<SettingsService>.Instance);

        var path = settings.EnsureUserSettingsFile();

        Assert.Equal(UserFile, path);
        Assert.Empty(SettingsJson.Parse(fileSystem.ReadAllText(path)));
    }

    public sealed class SampleOptions
    {
        public const int DefaultSize = 12;

        public int Size { get; set; } = DefaultSize;

        public string Name { get; set; } = "default";

        public bool Enabled { get; set; } = true;
    }
}
