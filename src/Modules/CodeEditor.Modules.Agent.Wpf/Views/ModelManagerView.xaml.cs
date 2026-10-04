using System.Windows;
using System.Windows.Input;
using CodeEditor.Modules.Agent.ViewModels.Models;

namespace CodeEditor.Modules.Agent.Wpf.Views;

/// <summary>The model manager over the chat: focuses search on open; Esc returns to the chat.</summary>
public sealed partial class ModelManagerView
{
    public ModelManagerView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(() => SearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ModelManagerViewModel manager)
        {
            manager.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
