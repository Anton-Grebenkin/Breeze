using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.ViewModels;

/// <summary>
/// A status bar item on the right, e.g. "Ln 12, Col 5", "UTF-8", "CRLF", "C#". Clickable when it has a command.
/// </summary>
public sealed partial class StatusBarItemViewModel(string id, int order) : ObservableObject
{
    public string Id { get; } = id;

    /// <summary>Order from left to right.</summary>
    public int Order { get; } = order;

    public string AutomationId => $"StatusBar.{Id}";

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ToolTip { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [ObservableProperty]
    public partial IRelayCommand? Command { get; set; }
}
