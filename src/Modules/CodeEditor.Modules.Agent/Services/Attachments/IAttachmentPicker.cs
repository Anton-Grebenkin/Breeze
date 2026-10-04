namespace CodeEditor.Modules.Agent.Services.Attachments;

/// <summary>Picks files to attach to a message: the system dialog in Agent.Wpf, a fake in tests.</summary>
public interface IAttachmentPicker
{
    /// <returns>The selected files; empty if the user cancelled.</returns>
    IReadOnlyList<string> PickFiles(string title);
}
