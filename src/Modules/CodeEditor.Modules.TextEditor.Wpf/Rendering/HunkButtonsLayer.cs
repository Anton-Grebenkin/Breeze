using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.UI.Themes;
using ICSharpCode.AvalonEdit.Rendering;

namespace CodeEditor.Modules.TextEditor.Wpf.Rendering;

/// <summary>
/// Accept and Reject buttons for each visible hunk: a layer above the text at the right edge of the hunk's first line,
/// as in Cursor. Placement is deferred after visual lines rebuild: the layer is a <see cref="TextView"/> child, and
/// changing its children during text measure would re-trigger measure endlessly. Per-hunk buttons are reused and
/// properties are set only when they actually change.
/// </summary>
internal sealed class HunkButtonsLayer : Canvas
{
    private const double RightMargin = 12;

    private readonly TextView _textView;
    private readonly Dictionary<int, FrameworkElement> _buttons = [];
    private IReadOnlyList<ChangeHunk> _hunks = [];
    private AgentChangesViewModel? _changes;
    private bool _placementQueued;

    public HunkButtonsLayer(TextView textView)
    {
        _textView = textView;
        IsHitTestVisible = true;
        _textView.VisualLinesChanged += (_, _) => QueuePlacement();
        _textView.SizeChanged += (_, _) => QueuePlacement();
    }

    public void Update(AgentChangesViewModel? changes, IReadOnlyList<ChangeHunk> hunks)
    {
        _changes = changes;
        _hunks = hunks;
        Children.Clear();
        _buttons.Clear();
        QueuePlacement();
    }

    private void QueuePlacement()
    {
        if (_placementQueued)
        {
            return;
        }

        _placementQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            _placementQueued = false;
            Place();
        }, DispatcherPriority.Loaded);
    }

    private void Place()
    {
        if (_changes is null || !_textView.VisualLinesValid || _textView.Document is null)
        {
            return;
        }

        var shown = new HashSet<int>();
        foreach (var hunk in _hunks)
        {
            if (Position(hunk) is not { } top)
            {
                continue;
            }

            shown.Add(hunk.Index);
            var panel = ButtonsFor(hunk);
            Set(panel, Math.Max(0, _textView.ActualWidth - panel.DesiredSize.Width - RightMargin), top);
        }

        foreach (var (index, panel) in _buttons)
        {
            var visibility = shown.Contains(index) ? Visibility.Visible : Visibility.Collapsed;
            if (panel.Visibility != visibility)
            {
                panel.Visibility = visibility;
            }
        }
    }

    private static void Set(FrameworkElement panel, double left, double top)
    {
        if (!GetLeft(panel).Equals(left))
        {
            SetLeft(panel, left);
        }

        if (!GetTop(panel).Equals(top))
        {
            SetTop(panel, top);
        }
    }

    // Hunk top: the removed-lines space above the first line, or the first added line itself.
    private double? Position(ChangeHunk hunk)
    {
        var lineCount = _textView.Document.LineCount;
        var anchor = hunk.RemovedLines.Count > 0 ? RemovedLinesSpacerGenerator.AnchorLine(hunk, lineCount) : hunk.NewStart;
        if (anchor < 1 || anchor > lineCount || _textView.GetVisualLine(anchor) is not { } line)
        {
            return null;
        }

        return RemovedLinesSpacerGenerator.IsBelow(hunk, lineCount)
            ? line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.TextBottom) - _textView.VerticalOffset
            : line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - _textView.VerticalOffset;
    }

    private FrameworkElement ButtonsFor(ChangeHunk hunk)
    {
        if (_buttons.TryGetValue(hunk.Index, out var existing))
        {
            return existing;
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(CreateButton(IconGlyphs.Check, Strings.AcceptHunk, Strings.AcceptHunkToolTip, _changes!.AcceptHunkCommand, hunk, "TextEditor.Hunk.Accept"));
        panel.Children.Add(CreateButton(IconGlyphs.Close, Strings.RejectHunk, Strings.RejectHunkToolTip, _changes.RejectHunkCommand, hunk, "TextEditor.Hunk.Reject"));
        var border = new Border
        {
            Child = panel,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2, 0, 2, 0),
        };
        border.SetResourceReference(Border.BackgroundProperty, ThemeKeys.WidgetBackground);
        border.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.WidgetBorder);
        _buttons[hunk.Index] = border;
        Children.Add(border);
        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return border;
    }

    private static Button CreateButton(string glyph, string text, string toolTip, System.Windows.Input.ICommand command, ChangeHunk hunk, string automationId)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new TextBlock { Text = glyph, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Icons");
        content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button
        {
            Content = content,
            Command = command,
            CommandParameter = hunk,
            ToolTip = toolTip,
            Padding = new Thickness(6, 1, 6, 1),
            Focusable = false,
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Button.Subtle");
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, text);
        return button;
    }
}
