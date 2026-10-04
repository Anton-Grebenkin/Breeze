using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>A file in the chat's changes panel: name, folder, status and the diff from the original.</summary>
public sealed partial class ChangedFileViewModel : ObservableObject
{
    public ChangedFileViewModel(FileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Change = change;
        var kind = change.IsCreated ? ProposedChangeKind.Create : change.IsDeleted ? ProposedChangeKind.Delete : ProposedChangeKind.Edit;
        Diff = new FileDiffViewModel(new FileChangePreview(kind, change.RelativePath, change.Original ?? string.Empty, change.Current ?? string.Empty));
    }

    public FileChange Change { get; }

    public string Name => Path.GetFileName(Change.RelativePath);

    public string Folder => Path.GetDirectoryName(Change.RelativePath)?.Replace('\\', '/') ?? string.Empty;

    public string Status => Change.IsCreated ? Strings.FileStatusNew : Change.IsDeleted ? Strings.FileStatusDeleted : string.Empty;

    public FileDiffViewModel Diff { get; }

    [ObservableProperty]
    public partial bool IsDiffVisible { get; set; }
}
