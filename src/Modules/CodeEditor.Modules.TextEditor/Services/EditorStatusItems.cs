using System.ComponentModel;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// Status bar items for the active text editor, as in VS Code: position, indentation, encoding, line endings,
/// language. Hidden when no text editor is active.
/// </summary>
public sealed class EditorStatusItems : IDisposable
{
    private readonly EditorAreaViewModel _editors;
    private readonly EditorSettings _settings;
    private readonly List<IDisposable> _registrations = [];
    private readonly StatusBarItemViewModel _position = new("editor.position", 10) { ToolTip = Strings.GoToLine };
    private readonly StatusBarItemViewModel _indentation = new("editor.indentation", 20) { ToolTip = Strings.IndentationToolTip };
    private readonly StatusBarItemViewModel _encoding = new("editor.encoding", 30) { ToolTip = Strings.EncodingToolTip };
    private readonly StatusBarItemViewModel _lineEnding = new("editor.lineEnding", 40) { ToolTip = Strings.LineEndingToolTip };
    private readonly StatusBarItemViewModel _language = new("editor.language", 50) { ToolTip = Strings.LanguageToolTip };
    private TextEditorViewModel? _active;

    public EditorStatusItems(EditorAreaViewModel editors, EditorSettings settings, StatusBarViewModel statusBar, ICommandService commands)
    {
        _editors = editors;
        _settings = settings;
        _indentation.Text = settings.IndentationText;
        _settings.PropertyChanged += OnSettingsChanged;

        // Clicking the position opens Go to Line, as in VS Code: the mouse path to Ctrl+G.
        _position.Command = new AsyncRelayCommand(async () => await commands.ExecuteAsync(ShellCommandIds.GoToLine));

        foreach (var item in Items)
        {
            _registrations.Add(statusBar.Add(item));
        }

        _editors.ActiveDocumentChanged += OnActiveDocumentChanged;
        Bind(null);
    }

    private IEnumerable<StatusBarItemViewModel> Items => [_position, _indentation, _encoding, _lineEnding, _language];

    public void Dispose()
    {
        _editors.ActiveDocumentChanged -= OnActiveDocumentChanged;
        _settings.PropertyChanged -= OnSettingsChanged;
        Bind(null);
        _registrations.ForEach(registration => registration.Dispose());
    }

    private void OnActiveDocumentChanged(object? sender, EventArgs e) => Bind(_editors.Active?.Editor as TextEditorViewModel);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorSettings.IndentationText))
        {
            _indentation.Text = _settings.IndentationText;
        }
    }

    private void Bind(TextEditorViewModel? editor)
    {
        if (_active is not null)
        {
            _active.PropertyChanged -= OnEditorPropertyChanged;
        }

        _active = editor;
        foreach (var item in Items)
        {
            item.IsVisible = editor is not null;
        }

        if (editor is null)
        {
            return;
        }

        editor.PropertyChanged += OnEditorPropertyChanged;
        _position.Text = editor.PositionText;
        _encoding.Text = editor.Document.Format.EncodingName;
        _lineEnding.Text = editor.Document.Format.LineEndingName;
        _language.Text = editor.Language;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextEditorViewModel.PositionText) && _active is not null)
        {
            _position.Text = _active.PositionText;
        }
    }
}
