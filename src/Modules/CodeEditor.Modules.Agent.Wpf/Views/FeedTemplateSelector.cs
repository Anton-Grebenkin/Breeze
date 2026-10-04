using System.Windows;
using System.Windows.Controls;
using CodeEditor.Modules.Agent.ViewModels.Activity;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>Feed item template: a work block as a chain, a message by its kind.</summary>
public sealed class FeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Block { get; set; }

    public DataTemplateSelector? Messages { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is ActivityBlockViewModel ? Block : Messages?.SelectTemplate(item, container);
}
