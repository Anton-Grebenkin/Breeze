namespace CodeEditor.Shell.ToolWindows;

/// <summary>Tool windows by id. Not thread-safe: UI thread only.</summary>
public sealed class ToolWindowRegistry : IToolWindowRegistry
{
    private readonly List<ToolWindowDefinition> _toolWindows = [];

    public event EventHandler? Changed;

    public IDisposable Register(ToolWindowDefinition toolWindow)
    {
        ArgumentNullException.ThrowIfNull(toolWindow);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolWindow.Id);

        if (_toolWindows.Exists(existing => existing.Id == toolWindow.Id))
        {
            throw new InvalidOperationException($"Tool window '{toolWindow.Id}' is already registered.");
        }

        _toolWindows.Add(toolWindow);
        Changed?.Invoke(this, EventArgs.Empty);

        return new Registration(this, toolWindow);
    }

    public IReadOnlyList<ToolWindowDefinition> GetAll(ToolWindowLocation location) =>
        [.. _toolWindows.Where(toolWindow => toolWindow.Location == location).OrderBy(toolWindow => toolWindow.Order)];

    private void Unregister(ToolWindowDefinition toolWindow)
    {
        if (_toolWindows.Remove(toolWindow))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Registration(ToolWindowRegistry registry, ToolWindowDefinition toolWindow) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                registry.Unregister(toolWindow);
            }
        }
    }
}
