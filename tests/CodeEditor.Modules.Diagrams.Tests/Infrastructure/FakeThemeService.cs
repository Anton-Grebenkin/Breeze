using CodeEditor.Shell.Theming;

namespace CodeEditor.Modules.Diagrams.Tests.Infrastructure;

/// <summary>Theme without WPF: a theme change raises an event, like the real service.</summary>
internal sealed class FakeThemeService : IThemeService
{
    public ThemeKind Current { get; private set; } = ThemeKind.Dark;

    public event EventHandler? Changed;

    public void Apply(ThemeKind theme)
    {
        Current = theme;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
