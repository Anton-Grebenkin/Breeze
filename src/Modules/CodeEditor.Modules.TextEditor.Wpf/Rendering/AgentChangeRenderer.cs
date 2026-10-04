using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.UI.Themes;
using ICSharpCode.AvalonEdit.Rendering;

namespace CodeEditor.Modules.TextEditor.Wpf.Rendering;

/// <summary>
/// Draws agent changes in the text: added lines get the inserted background; removed lines are drawn as text on the
/// removed background in the space a spacer reserved (<see cref="RemovedLinesSpacerGenerator"/>). Brushes are theme tokens.
/// </summary>
internal sealed class AgentChangeRenderer(TextView textView) : IBackgroundRenderer
{
    private const int TabWidth = 4;

    private IReadOnlyList<ChangeHunk> _hunks = [];

    public KnownLayer Layer => KnownLayer.Background;

    public void Update(IReadOnlyList<ChangeHunk> hunks) => _hunks = hunks;

    public void Draw(TextView view, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (_hunks.Count == 0 || !view.VisualLinesValid)
        {
            return;
        }

        var inserted = view.TryFindResource(ThemeKeys.DiffInsertedBackground) as Brush ?? Brushes.Transparent;
        var removed = view.TryFindResource(ThemeKeys.DiffRemovedBackground) as Brush ?? Brushes.Transparent;
        // Full width of the viewport plus scrolled text (TextView itself implements IScrollInfo).
        var width = view.ActualWidth + view.HorizontalOffset + ((System.Windows.Controls.Primitives.IScrollInfo)view).ExtentWidth;
        foreach (var line in view.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            foreach (var hunk in _hunks)
            {
                if (hunk.AddedCount > 0 && number >= hunk.NewStart && number <= hunk.LastLine)
                {
                    DrawTextRows(drawingContext, line, inserted, width);
                }

                if (hunk.RemovedLines.Count > 0 && number == RemovedLinesSpacerGenerator.AnchorLine(hunk, view.Document.LineCount))
                {
                    DrawRemoved(drawingContext, line, hunk, removed, width);
                }
            }
        }
    }

    private void DrawTextRows(DrawingContext drawingContext, VisualLine line, Brush brush, double width)
    {
        foreach (var textLine in line.TextLines)
        {
            var top = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop) - textView.VerticalOffset;
            var bottom = line.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom) - textView.VerticalOffset;
            drawingContext.DrawRectangle(brush, null, new Rect(-textView.HorizontalOffset, top, width, bottom - top));
        }
    }

    // The spacer sits above the line (removed lines preceded it) or below the file's last line.
    private void DrawRemoved(DrawingContext drawingContext, VisualLine line, ChangeHunk hunk, Brush brush, double width)
    {
        var rowHeight = textView.DefaultLineHeight;
        var top = RemovedLinesSpacerGenerator.IsBelow(hunk, textView.Document.LineCount)
            ? line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.TextBottom)
            : line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop);
        top -= textView.VerticalOffset;
        drawingContext.DrawRectangle(brush, null, new Rect(-textView.HorizontalOffset, top, width, rowHeight * hunk.RemovedLines.Count));

        var typeface = new Typeface(TextBlock.GetFontFamily(textView), TextBlock.GetFontStyle(textView), TextBlock.GetFontWeight(textView), TextBlock.GetFontStretch(textView));
        var foreground = TextBlock.GetForeground(textView);
        var dpi = VisualTreeHelper.GetDpi(textView).PixelsPerDip;
        for (var row = 0; row < hunk.RemovedLines.Count; row++)
        {
            var text = new FormattedText(hunk.RemovedLines[row].Replace("\t", new string(' ', TabWidth), StringComparison.Ordinal),
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, TextBlock.GetFontSize(textView), foreground, dpi);
            drawingContext.DrawText(text, new Point(-textView.HorizontalOffset, top + row * rowHeight));
        }
    }
}
