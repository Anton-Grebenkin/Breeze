using System.Globalization;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>A file row in a panel group: status letter, name, folder. A new snapshot updates the row in place.</summary>
public sealed partial class GitChangeItemViewModel : ObservableObject
{
    private readonly Action<object> _selected;

    internal GitChangeItemViewModel(GitFileChange change, Action<object> selected)
    {
        Change = change;
        _selected = selected;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(Letter), nameof(StatusText), nameof(ToolTip), nameof(IsDeleted))]
    public partial GitFileChange Change { get; internal set; }

    public string Path => Change.Path;

    public string Name => GitStatusLabels.FileName(Change.Path);

    public string Directory => GitStatusLabels.Directory(Change.Path);

    public GitFileStatus Status => Change.Status;

    public string Letter => GitStatusLabels.Letter(Change.Status);

    public string StatusText => GitStatusLabels.Describe(Change.Status);

    /// <summary>A deleted file is struck through, as in VS Code.</summary>
    public bool IsDeleted => Change.Status == GitFileStatus.Deleted;

    public bool IsStaged => Change.Group == GitChangeGroup.Staged;

    /// <summary>Only working tree changes can be discarded, not staged ones or conflicts.</summary>
    public bool CanDiscard => Change.Group == GitChangeGroup.Changes;

    public string ToolTip => Change.OriginalPath is { } original
        ? string.Format(CultureInfo.CurrentCulture, Strings.RenamedChangeToolTip, Change.Path, StatusText, original)
        : string.Format(CultureInfo.CurrentCulture, Strings.ChangeToolTip, Change.Path, StatusText);

    public string AutomationId => $"Git.Change.{Change.Group}.{Change.Path}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _selected(this);
        }
    }

    public override string ToString() => Change.Path;
}
