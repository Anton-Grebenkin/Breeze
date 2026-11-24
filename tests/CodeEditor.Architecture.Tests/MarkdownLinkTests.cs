using System.Runtime.ExceptionServices;
using System.Windows.Documents;
using CodeEditor.UI.Markdown;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// Markdown links as WPF elements. They live here because this is the test project that references the WPF assemblies
/// without starting the application.
/// </summary>
public sealed class MarkdownLinkTests
{
    // WPF throws on a hyperlink inside a hyperlink: an answer whose link text was a file path in backticks (which is
    // a link by itself) broke the rendering of the whole feed.
    [Fact]
    public void FilePathInsideLinkText_StaysPlain_OutsideItIsALink()
    {
        var links = OnStaThread(() => Links("Смотри [`src/App.cs:42`](https://example.com/a), [**https://b.test**](https://example.com/b) и `src/Other.cs`."));

        Assert.Equal([("https://example.com/a", false), ("https://example.com/b", false), ("src/Other.cs", false)], links);
    }

    private static List<(string Target, bool HasNestedLink)> Links(string markdown)
    {
        var renderer = new MarkdownRenderer(colorizer: null);
        var paragraph = Assert.IsType<Paragraph>(renderer.Convert(Assert.Single(renderer.Parse(markdown))));
        return [.. Descendants(paragraph.Inlines).OfType<Hyperlink>().Select(link => ((string)link.Tag, Descendants(link.Inlines).OfType<Hyperlink>().Any()))];
    }

    private static IEnumerable<Inline> Descendants(InlineCollection inlines) =>
        inlines.SelectMany(inline => inline is Span span ? [inline, .. Descendants(span.Inlines)] : (IEnumerable<Inline>)[inline]);

    // WPF text elements belong to the thread that created them, and that thread has to be STA.
    private static T OnStaThread<T>(Func<T> action)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result;
    }
}
