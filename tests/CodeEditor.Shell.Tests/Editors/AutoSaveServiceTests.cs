using CodeEditor.Core.Settings;
using CodeEditor.Shell.Editors;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Editors;

public sealed class AutoSaveServiceTests : IDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(1000);

    private readonly EditorAreaFixture _fixture = new();
    private readonly ManualTimeProvider _time = new();
    private readonly TestOptionsMonitor<FilesOptions> _options = new(new FilesOptions { AutoSave = FilesOptions.AutoSaveAfterDelay });
    private readonly AutoSaveService _autoSave;

    public AutoSaveServiceTests() =>
        _autoSave = new AutoSaveService(
            _fixture.Documents,
            new DocumentSaver(_fixture.Documents, _fixture.Dialogs, _fixture.StatusBar),
            _fixture.Context,
            _options,
            new InlineUiDispatcher(),
            _time);

    public void Dispose()
    {
        _autoSave.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task AfterDelay_SavesWhenTypingStops()
    {
        var tab = await _fixture.OpenAsync("a.cs");
        tab.Document.Buffer.Replace(0, 0, "// 1\n");
        _time.Advance(Delay / 2);
        tab.Document.Buffer.Replace(0, 0, "// 2\n");

        _time.Advance(Delay / 2);
        Assert.True(tab.Document.IsDirty);

        _time.Advance(Delay);
        await WaitSavedAsync(tab);
        Assert.StartsWith("// 2", _fixture.FileSystem.ReadAllText(EditorAreaFixture.PathOf("a.cs")), StringComparison.Ordinal);
    }

    // Zoom or theme writes settings.json and reloads all options: a pending save must survive that.
    [Fact]
    public async Task UnrelatedSettingsReload_KeepsPendingSave()
    {
        var tab = await _fixture.OpenAsync("a.cs");
        tab.Document.Buffer.Replace(0, 0, "// 1\n");

        _options.Set(new FilesOptions { AutoSave = FilesOptions.AutoSaveAfterDelay });
        _time.Advance(Delay);

        await WaitSavedAsync(tab);
    }

    [Fact]
    public async Task Off_DoesNotSave()
    {
        _options.Set(new FilesOptions());
        var tab = await _fixture.OpenAsync("a.cs");

        tab.Document.Buffer.Replace(0, 0, "x");
        _time.Advance(Delay * 5);

        Assert.True(tab.Document.IsDirty);
    }

    [Fact]
    public async Task OnFocusChange_SavesWhenTextLosesFocus()
    {
        _options.Set(new FilesOptions { AutoSave = FilesOptions.AutoSaveOnFocusChange });
        var tab = await _fixture.OpenAsync("a.cs");
        _fixture.Context.Set(EditorContextKeys.TextFocus, true);
        tab.Document.Buffer.Replace(0, 0, "x");

        _fixture.Context.Set(EditorContextKeys.TextFocus, false);

        await WaitSavedAsync(tab);
    }

    [Fact]
    public async Task ChangedOnDisk_IsNotOverwritten()
    {
        var tab = await _fixture.OpenAsync("a.cs");
        tab.Document.Buffer.Replace(0, 0, "x");
        _fixture.FileSystem.AddFile(EditorAreaFixture.PathOf("a.cs"), "чужая правка");
        _fixture.FileSystem.Touch(EditorAreaFixture.PathOf("a.cs"));
        _fixture.FileSystem.Watchers[0].Raise(new Core.Files.FileChange(EditorAreaFixture.PathOf("a.cs"), Core.Files.FileChangeKind.Changed));

        Assert.True(tab.Document.HasExternalChanges);
        _time.Advance(Delay);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal("чужая правка", _fixture.FileSystem.ReadAllText(EditorAreaFixture.PathOf("a.cs")));
    }

    // Saving runs in the background: wait for the dirty flag to clear.
    private static async Task WaitSavedAsync(EditorTabViewModel tab)
    {
        for (var i = 0; i < 100 && tab.Document.IsDirty; i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.False(tab.Document.IsDirty);
    }
}
