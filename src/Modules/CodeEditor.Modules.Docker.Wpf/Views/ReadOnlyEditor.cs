using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;

namespace CodeEditor.Modules.Docker.Wpf.Views;

/// <summary>
/// Read-only AvalonEdit text for logs and inspect: selection and copy, themed search (<c>Ctrl+F</c>), colors from theme
/// tokens. Links do not open: containers write the text, not the user. No undo history, since only the view changes
/// the text.
/// </summary>
internal static class ReadOnlyEditor
{
    private const string SelectionKey = "Brush.Editor.Selection";
    private const string FindMatchKey = "Brush.Editor.FindMatch";
    private const string CaretKey = "Brush.Text.Primary";
    private const string SearchPanelStyleKey = "SearchPanel.ReadOnly";

    public static SearchPanel Configure(TextEditor editor, FrameworkElement owner)
    {
        editor.IsReadOnly = true;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Document.UndoStack.SizeLimit = 0;
        editor.TextArea.SetResourceReference(TextArea.SelectionBrushProperty, SelectionKey);
        editor.TextArea.SelectionBorder = null;
        editor.TextArea.SelectionForeground = null;
        if (owner.TryFindResource(CaretKey) is Brush caret)
        {
            editor.TextArea.Caret.CaretBrush = caret;
        }

        var search = SearchPanel.Install(editor);
        search.Style = (Style)owner.FindResource(SearchPanelStyleKey);
        search.SetResourceReference(SearchPanel.MarkerBrushProperty, FindMatchKey);
        return search;
    }

    /// <summary>Opens search from the toolbar button; the selected text becomes the query, as with <c>Ctrl+F</c>.</summary>
    public static void OpenSearch(TextEditor editor, SearchPanel search)
    {
        if (!editor.TextArea.Selection.IsEmpty && !editor.TextArea.Selection.IsMultiline)
        {
            search.SearchPattern = editor.TextArea.Selection.GetText();
        }

        search.Open();
        search.Reactivate();
    }
}
