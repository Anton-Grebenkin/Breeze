using System.Windows;
using CodeEditor.Core.Threading;

namespace CodeEditor.Shell.Wpf.Services;

/// <summary>The app's UI thread via the WPF <see cref="System.Windows.Threading.Dispatcher"/>.</summary>
public sealed class WpfUiDispatcher(Application application) : IUiDispatcher
{
    public Task InvokeAsync(Action action)
    {
        var dispatcher = application.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    public void Post(Action action) => application.Dispatcher.BeginInvoke(action);
}
