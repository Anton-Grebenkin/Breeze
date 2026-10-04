using CodeEditor.Shell.Theming;

namespace CodeEditor.Shell.Tests.Infrastructure;

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
