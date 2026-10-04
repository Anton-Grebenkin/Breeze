using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// Files for the next message: added via the paperclip, the palette command or drag-and-drop. Text files go to the model
/// as content, images as pictures (<see cref="AttachmentReader"/>); other files are rejected and named in the status bar.
/// </summary>
public sealed partial class ChatAttachmentsViewModel(IAttachmentPicker picker, AttachmentReader reader, StatusBarViewModel statusBar) : ObservableObject
{
    public ObservableCollection<ChatAttachmentViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    /// <summary>Adds files from the dialog or a drop; skips duplicates and unsupported files.</summary>
    public async Task AddAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<string> rejected = [];
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Items.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var kind = await reader.ClassifyAsync(path, CancellationToken.None);
            if (kind == AttachmentKind.Unsupported)
            {
                rejected.Add(Path.GetFileName(path));
                continue;
            }

            Items.Add(new ChatAttachmentViewModel(path, reader.Display(path), kind == AttachmentKind.Image, Remove));
        }

        OnPropertyChanged(nameof(HasItems));
        if (rejected.Count > 0)
        {
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.AttachmentRejected, string.Join(", ", rejected));
        }
    }

    /// <summary>Returns the files for the outgoing message and clears the chips.</summary>
    public IReadOnlyList<string> Take()
    {
        var paths = Items.Select(item => item.Path).ToList();
        Items.Clear();
        OnPropertyChanged(nameof(HasItems));
        return paths;
    }

    [RelayCommand]
    private Task PickAsync() => AddAsync(picker.PickFiles(Strings.AttachFilesTitle));

    private void Remove(ChatAttachmentViewModel item)
    {
        Items.Remove(item);
        OnPropertyChanged(nameof(HasItems));
    }
}
