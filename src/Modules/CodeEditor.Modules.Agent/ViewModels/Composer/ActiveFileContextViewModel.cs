using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Editors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// The active tab's file as message context, like VS Code's "current file": a chip above the input box; clicking it
/// excludes or includes the file. The model gets only the path and reads what it needs with tools.
/// </summary>
public sealed partial class ActiveFileContextViewModel : ObservableObject, IDisposable
{
    private readonly EditorAreaViewModel _editors;
    private readonly IWorkspace _workspace;

    public ActiveFileContextViewModel(EditorAreaViewModel editors, IWorkspace workspace)
    {
        _editors = editors;
        _workspace = workspace;
        _editors.ActiveDocumentChanged += OnActiveDocumentChanged;
    }

    /// <summary>Active file path relative to the folder (full outside it); <c>null</c> when no tab is open.</summary>
    public string? RelativePath => _editors.Active?.FilePath is { } path ? Relative(path) : null;

    public string? FileName => _editors.Active?.Title;

    public bool HasFile => _editors.Active is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    public partial bool IsAttached { get; set; } = true;

    public string ToolTip => IsAttached
        ? string.Format(CultureInfo.CurrentCulture, Strings.ActiveFileShared, RelativePath)
        : Strings.ActiveFileNotShared;

    public void Dispose() => _editors.ActiveDocumentChanged -= OnActiveDocumentChanged;

    [RelayCommand]
    private void Toggle() => IsAttached = !IsAttached;

    private string Relative(string path)
    {
        var relative = _workspace.RelativePath(path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }

    private void OnActiveDocumentChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RelativePath));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(ToolTip));
    }
}
