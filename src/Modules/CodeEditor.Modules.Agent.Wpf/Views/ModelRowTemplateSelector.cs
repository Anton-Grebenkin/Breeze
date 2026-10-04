using System.Windows;
using System.Windows.Controls;
using CodeEditor.Modules.Agent.ViewModels.Models;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>Model manager row template: a vendor header or a model with a toggle.</summary>
public sealed class ModelRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Vendor { get; set; }

    public DataTemplate? Model { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is ModelRowViewModel { IsVendor: true } ? Vendor : Model;
}
