using System.Windows;
using System.Windows.Input;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Wpf.Search;

/// <summary>
/// Replace in AvalonEdit's search panel, like VS Code's widget: a replace row (expanded by the left arrow or
/// <c>Ctrl+H</c>), Replace (<c>Ctrl+Shift+1</c>, <c>Enter</c> in the replace box) and Replace All
/// (<c>Ctrl+Alt+Enter</c>). State lives in the panel's attached properties, logic in <see cref="SearchReplaceController"/>.
/// </summary>
public static class SearchReplace
{
    public static readonly DependencyProperty IsReplaceVisibleProperty = DependencyProperty.RegisterAttached(
        "IsReplaceVisible", typeof(bool), typeof(SearchReplace), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty ReplaceTextProperty = DependencyProperty.RegisterAttached(
        "ReplaceText", typeof(string), typeof(SearchReplace), new FrameworkPropertyMetadata(string.Empty));

    public static RoutedUICommand ToggleReplace { get; } = new(Strings.ToggleReplace, nameof(ToggleReplace), typeof(SearchReplace));

    public static RoutedUICommand ReplaceNext { get; } = new(
        Strings.Replace, nameof(ReplaceNext), typeof(SearchReplace), [new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Shift)]);

    public static RoutedUICommand ReplaceAll { get; } = new(
        Strings.ReplaceAll, nameof(ReplaceAll), typeof(SearchReplace), [new KeyGesture(Key.Enter, ModifierKeys.Control | ModifierKeys.Alt)]);

    public static bool GetIsReplaceVisible(DependencyObject element) => (bool)element.GetValue(IsReplaceVisibleProperty);

    public static void SetIsReplaceVisible(DependencyObject element, bool value) => element.SetValue(IsReplaceVisibleProperty, value);

    public static string GetReplaceText(DependencyObject element) => (string)element.GetValue(ReplaceTextProperty);

    public static void SetReplaceText(DependencyObject element, string value) => element.SetValue(ReplaceTextProperty, value);
}
