using System.Windows;

namespace CodeEditor.Shell.Wpf.Presentation;

/// <summary>
/// Maps ViewModel types to views. Modules register their pairs in their *.Wpf assembly, so the shell shows tool
/// window and editor content without knowing the module.
/// </summary>
public interface IViewRegistry
{
    /// <summary>A view without dependencies, created by its parameterless constructor.</summary>
    void Register<TViewModel, TView>()
        where TView : FrameworkElement, new();

    /// <summary>
    /// A view with dependencies: the factory gets them from DI at module registration and creates a view per ViewModel,
    /// with no service lookup from XAML.
    /// </summary>
    void Register<TViewModel>(Func<TViewModel, FrameworkElement> factory)
        where TViewModel : class;
}
