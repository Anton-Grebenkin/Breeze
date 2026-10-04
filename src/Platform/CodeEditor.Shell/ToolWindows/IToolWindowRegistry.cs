namespace CodeEditor.Shell.ToolWindows;

/// <summary>
/// Tool window registry. Modules don't know the layout, so the host can be replaced without touching them (ADR 0002).
/// </summary>
public interface IToolWindowRegistry
{
    event EventHandler? Changed;

    IDisposable Register(ToolWindowDefinition toolWindow);

    /// <summary>The area's tool windows by order, then by registration time.</summary>
    IReadOnlyList<ToolWindowDefinition> GetAll(ToolWindowLocation location);
}
