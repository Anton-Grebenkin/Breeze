using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.TextEditor.Commands;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class EditorFontZoomTests : IDisposable
{
    private const string SaveError = "Не удалось записать в настройки";

    private readonly TestOptionsMonitor<EditorOptions> _options = new(new EditorOptions());
    private readonly FakeSettingsService _settings = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly EditorSettings _editor;
    private readonly EditorFontZoom _zoom;

    public EditorFontZoomTests()
    {
        _editor = new EditorSettings(_options);
        _zoom = new EditorFontZoom(_editor, _settings, _statusBar);
    }

    public void Dispose() => _editor.Dispose();

    [Fact]
    public void ZoomIn_EnlargesFont_SavesZoom_AndShowsSize()
    {
        _zoom.ZoomIn();
        _zoom.ZoomIn();

        Assert.Equal(EditorSettings.DefaultFontSize + 2, _editor.FontSize);
        Assert.Equal(2.0, _settings.Written[EditorFontZoom.SettingKey]);
        Assert.Equal("Размер шрифта редактора: 16", _statusBar.Message);
    }

    [Fact]
    public void Reset_RemovesSavedZoom()
    {
        _zoom.ZoomOut();
        _zoom.Reset();

        Assert.Equal(EditorSettings.DefaultFontSize, _editor.FontSize);
        Assert.Null(Assert.Contains(EditorFontZoom.SettingKey, _settings.Written));
    }

    [Fact]
    public void Wheel_StepsByNotch_AndAddsUpTouchpadDeltas()
    {
        _zoom.Wheel(60);
        var afterHalfNotch = _editor.FontSize;
        _zoom.Wheel(60);
        var afterNotch = _editor.FontSize;
        _zoom.Wheel(-240);

        Assert.Equal((14.0, 15.0, 13.0), (afterHalfNotch, afterNotch, _editor.FontSize));
    }

    [Fact]
    public void AtLimit_ShowsSize_WithoutRewritingSettings()
    {
        _options.Set(new EditorOptions { FontSize = EditorSettings.MaxFontSize });

        _zoom.ZoomIn();

        Assert.Empty(_settings.Written);
        Assert.Equal("Размер шрифта редактора: 72", _statusBar.Message);
    }

    [Fact]
    public void BrokenSettings_FontStillChanges_AndErrorIsShown()
    {
        _settings.WriteError = SaveError;

        _zoom.ZoomIn();

        Assert.Equal(EditorSettings.DefaultFontSize + 1, _editor.FontSize);
        Assert.Equal(SaveError, _statusBar.Message);
    }

    // Ctrl+= belongs to the interface zoom, as in VS Code; the font zoom is in the palette and on Ctrl + wheel.
    [Fact]
    public async Task Commands_ZoomTheFont_WithoutKeys()
    {
        using var fixture = new EditorFixture();
        var commands = new CommandRegistry();
        var keybindings = new KeybindingRegistry();
        using var editorCommands = new TextEditorCommands(fixture.Editors, _zoom);
        editorCommands.Register(commands, keybindings, new MenuRegistry());
        var service = new CommandService(commands, new ContextKeyService(), NullLogger<CommandService>.Instance);

        await service.ExecuteAsync(TextEditorCommands.ZoomInId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EditorSettings.DefaultFontSize + 1, _editor.FontSize);
        Assert.Null(keybindings.FindForCommand(TextEditorCommands.ZoomInId));
        Assert.Empty(keybindings.GetByFirstChord(KeySequence.Parse("Ctrl+=").First));
    }
}
