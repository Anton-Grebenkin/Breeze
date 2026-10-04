using System.Globalization;
using System.Windows.Data;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>Maps a tool row icon to a Codicons glyph.</summary>
public sealed class ToolIconConverter : IValueConverter
{
    public static ToolIconConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Codicons.Glyph(value switch
    {
        AgentToolIcon.Read => "file",
        AgentToolIcon.Search => "search",
        AgentToolIcon.Folder => "folder",
        AgentToolIcon.Edit => "edit",
        AgentToolIcon.Create => "new-file",
        AgentToolIcon.Delete => "trash",
        AgentToolIcon.Move => "arrow-right",
        AgentToolIcon.Build => "tools",
        AgentToolIcon.Test => "beaker",
        AgentToolIcon.Terminal => "terminal",
        AgentToolIcon.Plan => "checklist",
        AgentToolIcon.Question => "question",
        AgentToolIcon.Diff => "diff",
        AgentToolIcon.Memory => "bookmark",
        AgentToolIcon.Explore => "telescope",
        AgentToolIcon.Git => "git-commit",
        AgentToolIcon.Web => "globe",
        AgentToolIcon.Container => "package",
        AgentToolIcon.Image => "file-media",
        _ => "gear",
    });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
