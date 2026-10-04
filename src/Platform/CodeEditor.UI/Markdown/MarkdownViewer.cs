using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace CodeEditor.UI.Markdown;

/// <summary>
/// Read-only Markdown text: selectable and copyable as a whole, clickable links, Copy on code blocks. While a reply
/// streams, it re-renders at most once per <see cref="RenderInterval"/>: each render is O(n), and the number of renders
/// is bounded by the reply's duration rather than its chunk count.
/// </summary>
public sealed class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownViewer),
        new PropertyMetadata(string.Empty, (viewer, _) => ((MarkdownViewer)viewer).ScheduleRender()));

    /// <summary>The code colorizer is inherited down the tree; the owning view sets it once.</summary>
    public static readonly DependencyProperty CodeColorizerProperty = DependencyProperty.RegisterAttached(
        "CodeColorizer", typeof(ICodeColorizer), typeof(MarkdownViewer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnCodeColorizerChanged));

    /// <summary>Text line height; <see cref="double.NaN"/> uses the font's. Lines never shrink below content, so large headings don't overlap.</summary>
    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(MarkdownViewer), new PropertyMetadata(double.NaN));

    /// <summary>
    /// Code font size in this text; <see cref="double.NaN"/> uses the theme's. Small text (reasoning) needs smaller code:
    /// the value goes into the viewer's resources under the theme key, so the document finds it first.
    /// </summary>
    public static readonly DependencyProperty CodeFontSizeProperty = DependencyProperty.Register(
        nameof(CodeFontSize), typeof(double), typeof(MarkdownViewer), new PropertyMetadata(double.NaN, OnCodeFontSizeChanged));

    public static readonly RoutedEvent LinkClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(LinkClicked), RoutingStrategy.Bubble, typeof(EventHandler<MarkdownLinkClickedEventArgs>), typeof(MarkdownViewer));

    private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(80);

    private readonly DispatcherTimer _timer;
    private readonly List<string> _sources = [];
    private readonly List<double> _bottomMargins = [];
    private FlowDocument? _document;
    private long _lastRender;
    private string? _rendered;

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        IsTabStop = false;
        _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = RenderInterval };
        _timer.Tick += (_, _) => Render();
        AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(OnHyperlinkClick));
        Loaded += (_, _) => Subscribe(GetCodeColorizer(this));
        Unloaded += (_, _) => Unsubscribe(GetCodeColorizer(this));
    }

    public event EventHandler<MarkdownLinkClickedEventArgs> LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public double CodeFontSize
    {
        get => (double)GetValue(CodeFontSizeProperty);
        set => SetValue(CodeFontSizeProperty, value);
    }

    public static ICodeColorizer? GetCodeColorizer(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (ICodeColorizer?)element.GetValue(CodeColorizerProperty);
    }

    public static void SetCodeColorizer(DependencyObject element, ICodeColorizer? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(CodeColorizerProperty, value);
    }

    /// <summary>Renders immediately without waiting for the interval (end of reply, tests).</summary>
    public void RenderNow() => Render();

    // The mouse wheel scrolls the chat feed, not the empty scroller inside a message.
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled || Parent is not UIElement parent)
        {
            return;
        }

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
    }

    private static void OnCodeFontSizeChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (MarkdownViewer)element;
        if (e.NewValue is double size && !double.IsNaN(size))
        {
            viewer.Resources[MarkdownInlines.CodeFontSizeKey] = size;
        }
        else
        {
            viewer.Resources.Remove(MarkdownInlines.CodeFontSizeKey);
        }
    }

    private static void OnCodeColorizerChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not MarkdownViewer viewer)
        {
            return;
        }

        viewer.Unsubscribe(e.OldValue as ICodeColorizer);
        if (viewer.IsLoaded)
        {
            viewer.Subscribe(e.NewValue as ICodeColorizer);
        }

        viewer.Rerender();
    }

    private void Subscribe(ICodeColorizer? colorizer)
    {
        if (colorizer is not null)
        {
            colorizer.Changed -= OnColorsChanged;
            colorizer.Changed += OnColorsChanged;
        }
    }

    private void Unsubscribe(ICodeColorizer? colorizer)
    {
        if (colorizer is not null)
        {
            colorizer.Changed -= OnColorsChanged;
        }
    }

    private void OnColorsChanged(object? sender, EventArgs e) => Rerender();

    // New code colors: rebuild all blocks.
    private void Rerender()
    {
        _rendered = null;
        _sources.Clear();
        _bottomMargins.Clear();
        _document?.Blocks.Clear();
        ScheduleRender();
    }

    private void ScheduleRender()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        if (Stopwatch.GetElapsedTime(_lastRender) >= RenderInterval)
        {
            Render();
        }
        else
        {
            _timer.Start();
        }
    }

    // One document for the viewer's lifetime: finished blocks stay, only the tail from the first changed block is
    // rebuilt. While streaming that's the last paragraph, so text above neither flickers nor shifts.
    private void Render()
    {
        _timer.Stop();
        _lastRender = Stopwatch.GetTimestamp();
        var markdown = Markdown ?? string.Empty;
        if (string.Equals(markdown, _rendered, StringComparison.Ordinal))
        {
            return;
        }

        _rendered = markdown;
        var renderer = new MarkdownRenderer(GetCodeColorizer(this));
        var blocks = renderer.Parse(markdown);
        var document = EnsureDocument();
        var unchanged = CountUnchanged(blocks);
        while (_sources.Count > unchanged)
        {
            document.Blocks.Remove(document.Blocks.LastBlock);
            _sources.RemoveAt(_sources.Count - 1);
            _bottomMargins.RemoveAt(_bottomMargins.Count - 1);
        }

        for (var index = unchanged; index < blocks.Count; index++)
        {
            var block = renderer.Convert(blocks[index]);
            document.Blocks.Add(block);
            _sources.Add(blocks[index].Source);
            _bottomMargins.Add(block.Margin.Bottom);
        }

        TrimLastMargin(document);
    }

    private int CountUnchanged(IReadOnlyList<MarkdownSourceBlock> blocks)
    {
        var count = 0;
        while (count < _sources.Count && count < blocks.Count && string.Equals(_sources[count], blocks[count].Source, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // The last block has no bottom margin so a message doesn't end with a gap; the former last block gets it back.
    private void TrimLastMargin(FlowDocument document)
    {
        var index = 0;
        foreach (var block in document.Blocks)
        {
            var bottom = index == _bottomMargins.Count - 1 ? 0 : _bottomMargins[index];
            if (block.Margin.Bottom != bottom)
            {
                block.Margin = block.Margin with { Bottom = bottom };
            }

            index++;
        }
    }

    private FlowDocument EnsureDocument()
    {
        if (_document is not null)
        {
            return _document;
        }

        _document = new FlowDocument { PagePadding = new Thickness(0), LineStackingStrategy = LineStackingStrategy.MaxHeight };
        Inherit(_document, FlowDocument.ForegroundProperty, nameof(Foreground));
        Inherit(_document, FlowDocument.FontFamilyProperty, nameof(FontFamily));
        Inherit(_document, FlowDocument.FontSizeProperty, nameof(FontSize));
        Inherit(_document, FlowDocument.LineHeightProperty, nameof(LineHeight));
        Document = _document;
        return _document;
    }

    // FlowDocument has its own default font and color; bind them to the viewer's, including after theme switches.
    private void Inherit(FlowDocument document, DependencyProperty property, string path) =>
        document.SetBinding(property, new System.Windows.Data.Binding(path) { Source = this });

    private void OnHyperlinkClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Hyperlink { Tag: { } tag })
        {
            return;
        }

        e.Handled = true;
        if (tag is CopyCodeRequest copy)
        {
            CopyToClipboard(copy.Code);
        }
        else if (tag is string target)
        {
            RaiseEvent(new MarkdownLinkClickedEventArgs(LinkClickedEvent, this, target));
        }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            // Another app holds the clipboard; the user can click again.
        }
    }
}
