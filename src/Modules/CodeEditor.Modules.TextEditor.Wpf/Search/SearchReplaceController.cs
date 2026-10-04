using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;

namespace CodeEditor.Modules.TextEditor.Wpf.Search;

/// <summary>
/// Replace for one editor's search panel. Matches come from an AvalonEdit strategy with the panel's own flags, so
/// regex groups (<c>$1</c>) work. Replace All is a single undo step.
/// </summary>
public sealed class SearchReplaceController
{
    private const string ReplaceTogglePart = "ReplaceToggle";
    private const string ReplaceTextBoxPart = "PART_replaceTextBox";

    private readonly SearchPanel _panel;
    private readonly TextArea _textArea;

    public SearchReplaceController(SearchPanel panel, TextArea textArea)
    {
        _panel = panel;
        _textArea = textArea;

        // The panel lives in the adorner layer, outside the TextArea tree, so replace commands bind to the panel itself.
        panel.CommandBindings.Add(new CommandBinding(SearchReplace.ToggleReplace, (_, _) => Toggle()));
        panel.CommandBindings.Add(new CommandBinding(SearchReplace.ReplaceNext, (_, _) => Replace(), CanReplace));
        panel.CommandBindings.Add(new CommandBinding(SearchReplace.ReplaceAll, (_, _) => ReplaceAllMatches(), CanReplace));
        panel.CommandBindings.Add(new CommandBinding(ApplicationCommands.Replace, (_, _) => OpenReplace()));
        textArea.CommandBindings.Add(new CommandBinding(ApplicationCommands.Replace, (_, _) => OpenReplace()));
        panel.Loaded += (_, _) => BindTemplateParts();
    }

    /// <summary>
    /// <c>Ctrl+H</c>: opens the panel with replace; a single-line selection becomes the query, as in VS Code.
    /// </summary>
    public void OpenReplace()
    {
        if (!_textArea.Selection.IsEmpty && !_textArea.Selection.IsMultiline)
        {
            _panel.SearchPattern = _textArea.Selection.GetText();
        }

        SearchReplace.SetIsReplaceVisible(_panel, true);
        _panel.Open();
        _panel.Dispatcher.InvokeAsync(_panel.Reactivate, DispatcherPriority.Input);
    }

    /// <summary>Replaces the selected match, if any, and moves to the next one.</summary>
    public void Replace()
    {
        if (CreateStrategy() is not { } strategy)
        {
            return;
        }

        var selection = _textArea.Selection.SurroundingSegment;
        if (selection is not null
            && strategy.FindNext(_textArea.Document, selection.Offset, selection.Length) is { } match
            && match.Offset == selection.Offset && match.Length == selection.Length)
        {
            _textArea.Document.Replace(match.Offset, match.Length, match.ReplaceWith(Replacement));
        }

        _panel.FindNext();
    }

    /// <summary>Replaces all matches as one undo step; returns their count.</summary>
    public int ReplaceAllMatches()
    {
        if (CreateStrategy() is not { } strategy)
        {
            return 0;
        }

        var document = _textArea.Document;
        var matches = strategy.FindAll(document, 0, document.TextLength).ToList();
        using (document.RunUpdate())
        {
            // From the end, so replacements don't shift unprocessed matches.
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                document.Replace(matches[i].Offset, matches[i].Length, matches[i].ReplaceWith(Replacement));
            }
        }

        return matches.Count;
    }

    /// <summary>
    /// Binds template parts to the panel's attached properties in code: WPF can't resolve a namespace-prefixed path
    /// inside deferred template content ("property not found on SearchPanel").
    /// </summary>
    private void BindTemplateParts()
    {
        if (_panel.Template?.FindName(ReplaceTogglePart, _panel) is ToggleButton toggle && !BindingOperations.IsDataBound(toggle, ToggleButton.IsCheckedProperty))
        {
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding { Path = new PropertyPath(SearchReplace.IsReplaceVisibleProperty), Source = _panel, Mode = BindingMode.TwoWay });
        }

        if (_panel.Template?.FindName(ReplaceTextBoxPart, _panel) is TextBox box && !BindingOperations.IsDataBound(box, TextBox.TextProperty))
        {
            box.SetBinding(TextBox.TextProperty, new Binding
            {
                Path = new PropertyPath(SearchReplace.ReplaceTextProperty),
                Source = _panel,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });
        }
    }

    private string Replacement => SearchReplace.GetReplaceText(_panel) ?? string.Empty;

    private void Toggle() => SearchReplace.SetIsReplaceVisible(_panel, !SearchReplace.GetIsReplaceVisible(_panel));

    private void CanReplace(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = !string.IsNullOrEmpty(_panel.SearchPattern);

    /// <summary>A strategy with the panel's flags; <c>null</c> for an invalid regex (the panel shows the error).</summary>
    private ISearchStrategy? CreateStrategy()
    {
        if (string.IsNullOrEmpty(_panel.SearchPattern))
        {
            return null;
        }

        try
        {
            return SearchStrategyFactory.Create(
                _panel.SearchPattern, !_panel.MatchCase, _panel.WholeWords, _panel.UseRegex ? SearchMode.RegEx : SearchMode.Normal);
        }
        catch (SearchPatternException)
        {
            return null;
        }
    }
}
