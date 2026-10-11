using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// Files for the next message: added via the paperclip, the palette command, drag-and-drop or paste. Text files go to
/// the model as content, images as pictures (<see cref="AttachmentReader"/>); other files are rejected and named in the
/// status bar. A pasted screenshot or long text is saved as a file first (<see cref="PastedAttachments"/>).
/// </summary>
public sealed partial class ChatAttachmentsViewModel(
    IAttachmentPicker picker,
    AttachmentReader reader,
    PastedAttachments pasted,
    IOptionsMonitor<AgentOptions> options,
    StatusBarViewModel statusBar) : ObservableObject
{
    public ObservableCollection<ChatAttachmentViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    /// <summary>Adds files from the dialog, a drop or the clipboard; skips duplicates and unsupported files.</summary>
    public async Task AddAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<string> rejected = [];
        var images = 0;
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

            images += kind == AttachmentKind.Image ? 1 : 0;
            Items.Add(new ChatAttachmentViewModel(path, reader.Display(path), kind == AttachmentKind.Image, Remove));
        }

        OnPropertyChanged(nameof(HasItems));
        if (rejected.Count > 0)
        {
            statusBar.Message = Format(Strings.AttachmentRejected, string.Join(", ", rejected));
        }
        else if (images > 0 && !ModelProfiles.For(options.CurrentValue.Model).SeesImages)
        {
            statusBar.Message = Format(Strings.ModelDoesNotSeeImages, ModelVendors.ShortName(options.CurrentValue.Model));
        }
    }

    /// <summary>A pasted or dropped picture (PNG): saved as a file and attached.</summary>
    public async Task AddImageAsync(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        await AddAsync([await Task.Run(() => pasted.SaveImage(png))]);
    }

    /// <summary>A long pasted text (<see cref="PastedAttachments.IsLongText"/>): attached as a file, not typed into the box.</summary>
    public async Task AddTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        await AddAsync([await Task.Run(() => pasted.SaveText(text))]);
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

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
