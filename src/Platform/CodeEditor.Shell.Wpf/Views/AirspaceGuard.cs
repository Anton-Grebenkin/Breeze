using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// WebView2 pages (browser, PDF, diagram preview) are native windows on top of WPF content, so the palette would go
/// under them. While the palette is open, visible hosts are hidden and then restored as they were, without reloading
/// the page. Hosts are found by walking the visual tree when the palette opens: O(n) elements.
/// </summary>
internal sealed class AirspaceGuard
{
    private readonly List<(HwndHost Host, Visibility Visibility)> _hidden = [];

    /// <summary>Hides visible hosts under <paramref name="root"/>.</summary>
    public void Hide(DependencyObject root)
    {
        Restore();
        foreach (var host in Hosts(root).Where(host => host.IsVisible))
        {
            _hidden.Add((host, host.Visibility));
            host.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
        }
    }

    /// <summary>Restores hidden hosts; bindings and styles on their visibility are preserved.</summary>
    public void Restore()
    {
        foreach (var (host, visibility) in _hidden)
        {
            host.SetCurrentValue(UIElement.VisibilityProperty, visibility);
        }

        _hidden.Clear();
    }

    private static IEnumerable<HwndHost> Hosts(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is HwndHost host)
            {
                yield return host;
                continue;
            }

            for (var index = VisualTreeHelper.GetChildrenCount(node) - 1; index >= 0; index--)
            {
                pending.Push(VisualTreeHelper.GetChild(node, index));
            }
        }
    }
}
