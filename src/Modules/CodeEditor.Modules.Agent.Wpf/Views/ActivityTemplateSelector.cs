using System.Windows;
using System.Windows.Controls;
using CodeEditor.Modules.Agent.ViewModels.Activity;
using CodeEditor.Modules.Agent.ViewModels.Chat;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>Chain step template: reasoning, exploration group, tool, progress line or check.</summary>
public sealed class ActivityTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Thinking { get; set; }

    public DataTemplate? Exploration { get; set; }

    public DataTemplate? Tool { get; set; }

    public DataTemplate? Progress { get; set; }

    public DataTemplate? Status { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        ThinkingItemViewModel => Thinking,
        ExplorationGroupViewModel => Exploration,
        ActivityEntryViewModel { Message.Kind: ChatMessageKind.Tool } => Tool,
        ActivityEntryViewModel { Message.Kind: ChatMessageKind.Progress } => Progress,
        ActivityEntryViewModel => Status,
        _ => null,
    };
}
