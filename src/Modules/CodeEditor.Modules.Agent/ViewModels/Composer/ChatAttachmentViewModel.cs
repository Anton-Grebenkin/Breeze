using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// A file attached to the next message, shown as a chip above the input box. It has its own Remove command so the
/// markup needs no binding to the parent, which causes binding errors in dynamic items.
/// </summary>
/// <param name="display">Tooltip path: relative to the folder, or full when outside it.</param>
public sealed class ChatAttachmentViewModel(string path, string display, bool isImage, Action<ChatAttachmentViewModel> remove)
{
    public string Path { get; } = path;

    public string Display { get; } = display;

    public string Name { get; } = System.IO.Path.GetFileName(path);

    public bool IsImage { get; } = isImage;

    public string AutomationId => "Agent.Attachment." + Name;

    public string RemoveAutomationId => AutomationId + ".Remove";

    public IRelayCommand RemoveCommand => field ??= new RelayCommand(() => remove(this));
}
