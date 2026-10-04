using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Modules.TextEditor.Wpf.Buffers;
using CodeEditor.Modules.TextEditor.Wpf.Highlighting;
using CodeEditor.Modules.TextEditor.Wpf.Rendering;
using CodeEditor.Modules.TextEditor.Wpf.Search;
using CodeEditor.Shell.Editors;
using CodeEditor.UI.Controls;
using CodeEditor.UI.Themes;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;

namespace CodeEditor.Modules.TextEditor.Wpf.Views;

/// <summary>
/// AvalonEdit editor over a document buffer. Visual logic only: theme-colored highlighting, selection and current
/// line brushes from tokens; caret position and focus go to the ViewModel; Ctrl + wheel zooms the font. The view lives
/// as long as its tab, so caret and scroll position survive tab switches.
/// </summary>
public sealed partial class TextEditorView
{
    private const string CodeFontKey = "Font.Code";

    private static readonly Pen NoBorder = CreateNoBorder();

    private readonly ThemedHighlighting _highlighting;
    private readonly AgentChangeDecorator _agentChanges;
    private readonly EditorFontZoom _fontZoom;
    private TextEditorViewModel? _viewModel;

    public TextEditorView(ThemedHighlighting highlighting, EditorFontZoom fontZoom)
    {
        _highlighting = highlighting;
        _fontZoom = fontZoom;
        InitializeComponent();
        ConfigureEditor();
        _agentChanges = new AgentChangeDecorator(Editor.TextArea.TextView);

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdatePosition();
        Editor.TextArea.SelectionChanged += (_, _) => UpdatePosition();
        Editor.TextArea.GotKeyboardFocus += (_, _) => _viewModel?.SetFocused(true);
        Editor.TextArea.LostKeyboardFocus += (_, _) => _viewModel?.SetFocused(false);

        // Over the editor Ctrl + wheel zooms the font, not the whole window.
        WheelZoom.SetHasOwnZoom(Editor, true);
        Editor.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    // Subscribe to shared services only while in the tree, so a closed tab isn't kept alive.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _highlighting.Changed += OnThemeChanged;
        if (_viewModel is not null)
        {
            _viewModel.Settings.PropertyChanged += OnSettingsChanged;
            ApplySettings();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _highlighting.Changed -= OnThemeChanged;
        if (_viewModel is not null)
        {
            _viewModel.Settings.PropertyChanged -= OnSettingsChanged;
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => ApplySettings();

    /// <summary>Settings that aren't AvalonEdit dependency properties: font, indentation, visible whitespace.</summary>
    private void ApplySettings()
    {
        if (_viewModel is null)
        {
            return;
        }

        var settings = _viewModel.Settings;
        var options = Editor.Options;
        options.ConvertTabsToSpaces = settings.InsertSpaces;
        options.IndentationSize = settings.TabSize;
        options.ShowSpaces = settings.ShowWhitespace;
        options.ShowTabs = settings.ShowWhitespace;

        if (settings.FontFamily is { } family)
        {
            Editor.FontFamily = new FontFamily(family);
        }
        else
        {
            Editor.SetResourceReference(FontFamilyProperty, CodeFontKey);
        }
    }

    private void ConfigureEditor()
    {
        var options = Editor.Options;
        options.HighlightCurrentLine = true;
        options.EnableHyperlinks = false;
        options.EnableEmailHyperlinks = false;
        options.AllowScrollBelowDocument = true;

        var textArea = Editor.TextArea;
        textArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, ThemeKeys.EditorSelection);
        textArea.SelectionForeground = null;
        textArea.SelectionBorder = null;
        textArea.SelectionCornerRadius = 0;
        textArea.TextView.SetResourceReference(TextView.CurrentLineBackgroundProperty, ThemeKeys.EditorLineHighlight);

        // AvalonEdit replaces null with its own green border, so set an explicit transparent pen.
        textArea.TextView.CurrentLineBorder = NoBorder;

        // Set the match brush only after Install: AvalonEdit crashes if a style sets it earlier.
        var search = SearchPanel.Install(textArea);
        search.SetResourceReference(SearchPanel.MarkerBrushProperty, ThemeKeys.EditorFindMatch);

        // Replace (Ctrl+H): the controller is kept alive by the panel's and text area's command bindings.
        _ = new SearchReplaceController(search, textArea);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.NavigationRequested -= OnNavigationRequested;
        }

        _viewModel = e.NewValue as TextEditorViewModel;
        if (_viewModel?.Document.Buffer is not AvalonTextBuffer buffer)
        {
            _agentChanges.Attach(null);
            return;
        }

        Editor.Document = buffer.Document;
        _agentChanges.Attach(_viewModel.AgentChanges);
        ApplyHighlighting();
        ApplySettings();
        if (IsLoaded)
        {
            // Already in the tree: OnLoaded ran without a ViewModel and didn't subscribe.
            _viewModel.Settings.PropertyChanged -= OnSettingsChanged;
            _viewModel.Settings.PropertyChanged += OnSettingsChanged;
        }

        UpdatePosition();
        _viewModel.NavigationRequested += OnNavigationRequested;
        OnNavigationRequested(this, EventArgs.Empty);
    }

    // Navigate after layout: a new view has no height to scroll yet, and the closing palette returns focus to
    // where it was opened from.
    private void OnNavigationRequested(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(ApplyPendingNavigation, DispatcherPriority.Input);

    private void ApplyPendingNavigation()
    {
        if (_viewModel?.TakeNavigation() is not { } request)
        {
            return;
        }

        if (request.Location is { } location)
        {
            Reveal(location);
        }

        if (request.Focus)
        {
            Editor.TextArea.Focus();
        }
    }

    /// <summary>
    /// Selects the location with the caret at its end (like VS Code search); centers the line if it was off-screen.
    /// </summary>
    private void Reveal(EditorLocation location)
    {
        var document = Editor.Document;
        var line = document.GetLineByNumber(Math.Clamp(location.Line, 1, document.LineCount));
        var start = line.Offset + Math.Min(location.Column - 1, line.Length);
        var length = Math.Min(location.Length, document.TextLength - start);

        var textArea = Editor.TextArea;
        textArea.Caret.Offset = start + length;
        if (length > 0)
        {
            textArea.Selection = Selection.Create(textArea, start, start + length);
        }
        else
        {
            textArea.ClearSelection();
        }

        Editor.ScrollTo(line.LineNumber, start - line.Offset + 1, VisualYPosition.LineMiddle, textArea.TextView.ActualHeight / 2, 0.3);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        e.Handled = true;
        _fontZoom.Wheel(e.Delta);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyHighlighting();

    private void ApplyHighlighting()
    {
        if (_viewModel is not null)
        {
            Editor.SyntaxHighlighting = _highlighting.ForFile(_viewModel.Document.FilePath);
        }
    }

    private void UpdatePosition()
    {
        if (_viewModel is null)
        {
            return;
        }

        var caret = Editor.TextArea.Caret;
        _viewModel.CaretLine = caret.Line;
        _viewModel.CaretColumn = caret.Column;
        _viewModel.SelectionStart = Editor.TextArea.Selection.SurroundingSegment?.Offset ?? Editor.CaretOffset;
        _viewModel.SelectionLength = Editor.TextArea.Selection.Length;
        _viewModel.SelectionLines = Editor.TextArea.Selection.SurroundingSegment is { } segment
            ? (Editor.Document.GetLineByOffset(segment.Offset).LineNumber, Editor.Document.GetLineByOffset(segment.EndOffset).LineNumber)
            : null;
    }

    private static Pen CreateNoBorder()
    {
        var pen = new Pen(Brushes.Transparent, 0);
        pen.Freeze();
        return pen;
    }
}
