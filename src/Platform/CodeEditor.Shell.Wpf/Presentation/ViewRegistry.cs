using System.Windows;

namespace CodeEditor.Shell.Wpf.Presentation;

/// <summary>
/// Registers an implicit <see cref="DataTemplate"/> in application resources, so any <c>ContentControl</c> holding a
/// ViewModel of that type creates the right view.
/// </summary>
public sealed class ViewRegistry(Application application) : IViewRegistry
{
    public void Register<TViewModel, TView>()
        where TView : FrameworkElement, new() =>
        Add(typeof(TViewModel), new FrameworkElementFactory(typeof(TView)));

    public void Register<TViewModel>(Func<TViewModel, FrameworkElement> factory)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var host = new FrameworkElementFactory(typeof(ViewFactoryHost));
        host.SetValue(ViewFactoryHost.FactoryProperty, new Func<object, FrameworkElement>(viewModel => factory((TViewModel)viewModel)));
        Add(typeof(TViewModel), host);
    }

    private void Add(Type viewModelType, FrameworkElementFactory visualTree)
    {
        var template = new DataTemplate(viewModelType) { VisualTree = visualTree };
        template.Seal();
        application.Resources[new DataTemplateKey(viewModelType)] = template;
    }
}
