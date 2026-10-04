using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Zoom;

namespace CodeEditor.Shell.ViewModels;

/// <summary>
/// Root ViewModel of the main window. It only composes the parts' ViewModels and has no logic of its own.
/// </summary>
public sealed class MainWindowViewModel(
    TitleBarViewModel titleBar,
    ActivityBarViewModel activityBar,
    WorkbenchLayout layout,
    WelcomeViewModel welcome,
    EditorAreaHost editorArea,
    StatusBarViewModel statusBar,
    CommandPaletteViewModel palette,
    WindowZoom zoom)
{
    public const string ProductName = "Breeze";

    public string Title => ProductName;

    public TitleBarViewModel TitleBar { get; } = titleBar;

    public ActivityBarViewModel ActivityBar { get; } = activityBar;

    public WorkbenchLayout Layout { get; } = layout;

    public WelcomeViewModel Welcome { get; } = welcome;

    public EditorAreaHost EditorArea { get; } = editorArea;

    public StatusBarViewModel StatusBar { get; } = statusBar;

    public CommandPaletteViewModel Palette { get; } = palette;

    public WindowZoom Zoom { get; } = zoom;
}
