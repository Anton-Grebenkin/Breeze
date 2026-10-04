using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CodeEditor.UI.Themes;

namespace CodeEditor.UI.Controls;

/// <summary>Text with highlighted characters: fuzzy matches in the palette and quick open.</summary>
public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
        nameof(SourceText), typeof(string), typeof(HighlightedTextBlock),
        new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty HighlightsProperty = DependencyProperty.Register(
        nameof(Highlights), typeof(IReadOnlyList<int>), typeof(HighlightedTextBlock),
        new PropertyMetadata(null, OnContentChanged));

    public string SourceText
    {
        get => (string)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>Indices of highlighted characters, ascending.</summary>
    public IReadOnlyList<int>? Highlights
    {
        get => (IReadOnlyList<int>?)GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    private static void OnContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((HighlightedTextBlock)sender).Rebuild();

    /// <summary>Builds runs, merging consecutive characters with the same highlight state. O(text length).</summary>
    private void Rebuild()
    {
        Inlines.Clear();
        var text = SourceText ?? string.Empty;
        var highlights = Highlights;

        if (highlights is null || highlights.Count == 0)
        {
            Inlines.Add(new Run(text));
            return;
        }

        var position = 0;
        var index = 0;
        while (position < text.Length)
        {
            var isHighlighted = index < highlights.Count && highlights[index] == position;
            var start = position;

            while (position < text.Length && (index < highlights.Count && highlights[index] == position) == isHighlighted)
            {
                if (isHighlighted)
                {
                    index++;
                }

                position++;
            }

            Inlines.Add(CreateRun(text[start..position], isHighlighted));
        }
    }

    private static Run CreateRun(string text, bool isHighlighted)
    {
        var run = new Run(text);
        if (isHighlighted)
        {
            run.FontWeight = FontWeights.SemiBold;
            run.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MatchHighlightForeground);
        }

        return run;
    }
}
