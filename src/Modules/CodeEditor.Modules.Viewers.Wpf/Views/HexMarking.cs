using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// Dump column text with a marked byte: after go to offset the byte is highlighted with the find match color:
/// <c>views:HexMarking.Text="{Binding Hex}" views:HexMarking.Marked="{Binding Marked}"</c>. An unmarked row is plain
/// text with no extra elements.
/// </summary>
public static class HexMarking
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(HexMarking), new PropertyMetadata(string.Empty, OnChanged));

    /// <summary>The marked byte of the row (0-15), or <see cref="HexRow.NoMark"/>.</summary>
    public static readonly DependencyProperty MarkedProperty = DependencyProperty.RegisterAttached(
        "Marked", typeof(int), typeof(HexMarking), new PropertyMetadata(HexRow.NoMark, OnChanged));

    /// <summary>The byte column (two digits per byte) or the character column (one character per byte).</summary>
    public static readonly DependencyProperty IsBytesProperty = DependencyProperty.RegisterAttached(
        "IsBytes", typeof(bool), typeof(HexMarking), new PropertyMetadata(true, OnChanged));

    private const int HexDigitsPerByte = 2;

    public static string GetText(TextBlock block) => (string)block.GetValue(TextProperty);

    public static void SetText(TextBlock block, string value) => block.SetValue(TextProperty, value);

    public static int GetMarked(TextBlock block) => (int)block.GetValue(MarkedProperty);

    public static void SetMarked(TextBlock block, int value) => block.SetValue(MarkedProperty, value);

    public static bool GetIsBytes(TextBlock block) => (bool)block.GetValue(IsBytesProperty);

    public static void SetIsBytes(TextBlock block, bool value) => block.SetValue(IsBytesProperty, value);

    private static void OnChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is TextBlock block)
        {
            Build(block, GetText(block) ?? string.Empty, GetMarked(block), GetIsBytes(block));
        }
    }

    private static void Build(TextBlock block, string text, int marked, bool isBytes)
    {
        var start = isBytes ? HexFormatter.HexColumn(Math.Max(marked, 0)) : marked;
        var length = isBytes ? HexDigitsPerByte : 1;
        if (marked < 0 || start + length > text.Length)
        {
            block.Text = text;
            return;
        }

        var mark = new Run(text.Substring(start, length));
        mark.SetResourceReference(TextElement.BackgroundProperty, ThemeKeys.EditorFindMatch);
        block.Inlines.Clear();
        block.Inlines.Add(new Run(text[..start]));
        block.Inlines.Add(mark);
        block.Inlines.Add(new Run(text[(start + length)..]));
    }
}
