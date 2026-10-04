namespace CodeEditor.Core.Threading;

/// <summary>
/// Marshals work to the UI thread: watcher events and background results change view models only there.
/// The WPF dispatcher in the app, immediate execution in tests.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Runs the action on the UI thread and waits for it; runs inline when already on the UI thread.</summary>
    Task InvokeAsync(Action action);

    /// <summary>Queues the action on the UI thread without waiting.</summary>
    void Post(Action action);
}
