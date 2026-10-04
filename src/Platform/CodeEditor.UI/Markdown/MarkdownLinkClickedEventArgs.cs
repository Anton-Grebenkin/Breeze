using System.Windows;

namespace CodeEditor.UI.Markdown;

/// <summary>A Markdown link click: a web address or a project file path (<c>src/App.cs:42</c>).</summary>
public sealed class MarkdownLinkClickedEventArgs(RoutedEvent routedEvent, object source, string target) : RoutedEventArgs(routedEvent, source)
{
    public string Target { get; } = target;
}
