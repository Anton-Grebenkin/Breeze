namespace CodeEditor.Modules.Agent.Services.Attachments;

/// <summary>No picker without a window (benchmark, logic tests); the app replaces it with the Agent.Wpf dialog.</summary>
internal sealed class EmptyAttachmentPicker : IAttachmentPicker
{
    public IReadOnlyList<string> PickFiles(string title) => [];
}
