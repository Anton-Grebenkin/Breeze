using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using CodeEditor.Modules.TextEditor.Services.Agent;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CodeEditor.Modules.TextEditor.Wpf.Rendering;

/// <summary>
/// Reserves space for lines the agent removed: a zero-length element as tall as those lines is inserted at the start
/// of the line they preceded, pushing its text down, like Cursor's inline diff. A removal at the end of the file
/// reserves space below the last line. <see cref="AgentChangeRenderer"/> draws the lines. The caret skips the element.
/// </summary>
internal sealed class RemovedLinesSpacerGenerator : VisualLineElementGenerator
{
    private IReadOnlyList<ChangeHunk> _hunks = [];

    public void Update(IReadOnlyList<ChangeHunk> hunks) => _hunks = hunks;

    /// <summary>A removal at the end of the file: the space goes below the last line.</summary>
    public static bool IsBelow(ChangeHunk hunk, int lineCount) => hunk.IsRemoval && hunk.NewStart > lineCount;

    /// <summary>The document line the removed-lines space is attached to.</summary>
    public static int AnchorLine(ChangeHunk hunk, int lineCount) => IsBelow(hunk, lineCount) ? lineCount : hunk.NewStart;

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var next = -1;
        foreach (var hunk in _hunks)
        {
            if (AnchorOffset(hunk) is { } offset && offset >= startOffset && (next < 0 || offset < next))
            {
                next = offset;
            }
        }

        return next;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var hunk = _hunks.FirstOrDefault(candidate => AnchorOffset(candidate) == offset);
        return hunk is null ? null : new Spacer(hunk.RemovedLines.Count, IsBelow(hunk, CurrentContext.Document.LineCount), CurrentContext.TextView);
    }

    private int? AnchorOffset(ChangeHunk hunk)
    {
        if (hunk.RemovedLines.Count == 0)
        {
            return null;
        }

        var document = CurrentContext.Document;
        var anchor = AnchorLine(hunk, document.LineCount);
        if (anchor < 1 || anchor > document.LineCount)
        {
            return null;
        }

        var line = document.GetLineByNumber(anchor);
        return IsBelow(hunk, document.LineCount) ? line.EndOffset : line.Offset;
    }

    private sealed class Spacer(int rows, bool below, TextView textView) : VisualLineElement(1, 0)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var rowHeight = textView.DefaultLineHeight;
            var height = rowHeight * (rows + 1);
            var baseline = (below ? 0 : rows * rowHeight) + textView.DefaultBaseline;
            return new SpacerRun(context.GlobalTextRunProperties, height, baseline);
        }

        public override int GetNextCaretPosition(int visualColumn, System.Windows.Documents.LogicalDirection direction, CaretPositioningMode mode) => -1;
    }

    private sealed class SpacerRun(TextRunProperties properties, double height, double baseline) : TextEmbeddedObject
    {
        public override LineBreakCondition BreakBefore => LineBreakCondition.BreakDesired;

        public override LineBreakCondition BreakAfter => LineBreakCondition.BreakDesired;

        public override bool HasFixedSize => true;

        public override CharacterBufferReference CharacterBufferReference => default;

        public override int Length => 1;

        public override TextRunProperties Properties => properties;

        public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth) => new(0, height, baseline);

        public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways) => new(0, 0, 0, height);

        public override void Draw(DrawingContext drawingContext, Point origin, bool rightToLeft, bool sideways)
        {
        }
    }
}
