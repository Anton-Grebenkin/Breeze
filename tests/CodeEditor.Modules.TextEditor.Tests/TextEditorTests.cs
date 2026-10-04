using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class TextEditorTests
{
    [Fact]
    public void FontZoom_StaysWithinBounds()
    {
        var settings = CreateSettings();

        settings.SetFontZoom(1000);
        Assert.Equal((EditorSettings.MaxFontSize, EditorSettings.MaxFontSize - EditorSettings.DefaultFontSize), (settings.FontSize, settings.FontZoom));

        settings.SetFontZoom(-1000);
        Assert.Equal(EditorSettings.MinFontSize, settings.FontSize);

        settings.SetFontZoom(0);
        Assert.Equal(EditorSettings.DefaultFontSize, settings.FontSize);
    }

    [Fact]
    public void Settings_FollowEditorOptions_WithFontZoom()
    {
        var options = new TestOptionsMonitor<EditorOptions>(new EditorOptions());
        using var settings = new EditorSettings(options);
        var changed = new List<string?>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        options.Set(new EditorOptions
        {
            FontSize = 20,
            FontZoom = 1,
            FontFamily = "'Fira Code', Consolas",
            TabSize = 2,
            InsertSpaces = false,
            WordWrap = "on",
            LineNumbers = "off",
            RenderWhitespace = "all",
        });

        Assert.Equal(21, settings.FontSize);
        Assert.Equal("Fira Code, Consolas", settings.FontFamily);
        Assert.Equal("Табуляция: 2", settings.IndentationText);
        Assert.True(settings.WordWrap);
        Assert.False(settings.ShowLineNumbers);
        Assert.True(settings.ShowWhitespace);
        Assert.Contains(nameof(EditorSettings.IndentationText), changed);

        settings.SetFontZoom(0);
        Assert.Equal(20, settings.FontSize);
    }

    [Fact]
    public void Settings_ClampInvalidValues()
    {
        using var settings = new EditorSettings(new TestOptionsMonitor<EditorOptions>(new EditorOptions { FontSize = 500, TabSize = 0, WordWrap = "maybe" }));

        Assert.Equal(EditorSettings.MaxFontSize, settings.FontSize);
        Assert.Equal(1, settings.TabSize);
        Assert.False(settings.WordWrap);
        Assert.Null(settings.FontFamily);
    }

    private static EditorSettings CreateSettings() => new(new TestOptionsMonitor<EditorOptions>(new EditorOptions()));

    [Theory]
    [InlineData(@"C:\src\Program.cs", "C#")]
    [InlineData(@"C:\src\App.XAML", "XAML")]
    [InlineData(@"C:\docs\arch.mmd", "Mermaid")]
    [InlineData(@"C:\src\readme", "Обычный текст")]
    public void Language_ComesFromExtension(string path, string language)
    {
        Assert.Equal(language, LanguageNames.ForFile(path));
    }

    [Fact]
    public void PositionText_ShowsSelection()
    {
        var context = new ContextKeyService();
        var editor = new TextEditorViewModel(new StubDocument(), CreateSettings(), new EditorFocus(context))
        {
            CaretLine = 12,
            CaretColumn = 5,
        };

        Assert.Equal("Стр. 12, стлб. 5", editor.PositionText);

        editor.SelectionLength = 42;
        Assert.Equal("Стр. 12, стлб. 5 (выделено 42)", editor.PositionText);

        editor.SetFocused(true);
        Assert.Equal(true, context.GetValue("editorFocus"));
    }

    private sealed class StubDocument : IDocument
    {
        public string FilePath => @"C:\src\Program.cs";

        public string Name => "Program.cs";

        public ITextBuffer Buffer { get; } = new TestTextBuffer("class Program {}");

        public TextFileFormat Format => TextFileFormat.Default;

        public bool IsDirty => Buffer.IsModified;

        public bool HasExternalChanges => false;

        public bool IsDeletedOnDisk => false;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }
    }
}
