using System.Windows;
using System.Windows.Controls;

namespace CodeEditor.Shell.Wpf.Presentation;

/// <summary>
/// Data template node that creates the view for its ViewModel with the module's factory. The view is created once per
/// ViewModel and inherits it as <c>DataContext</c>.
/// </summary>
internal sealed class ViewFactoryHost : ContentControl
{
    public static readonly DependencyProperty FactoryProperty = DependencyProperty.Register(
        nameof(Factory), typeof(Func<object, FrameworkElement>), typeof(ViewFactoryHost),
        new PropertyMetadata(null, (host, _) => ((ViewFactoryHost)host).Rebuild()));

    public ViewFactoryHost()
    {
        Focusable = false;
        DataContextChanged += (_, _) => Rebuild();
    }

    public Func<object, FrameworkElement>? Factory
    {
        get => (Func<object, FrameworkElement>?)GetValue(FactoryProperty);
        set => SetValue(FactoryProperty, value);
    }

    private void Rebuild() =>
        Content = Factory is { } factory && DataContext is { } viewModel ? factory(viewModel) : null;
}
