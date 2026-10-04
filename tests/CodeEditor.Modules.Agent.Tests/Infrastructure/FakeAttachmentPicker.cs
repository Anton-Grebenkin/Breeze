using CodeEditor.Modules.Agent.Services.Attachments;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>Attachment picker that "picks" preset files; an empty list means cancel.</summary>
internal sealed class FakeAttachmentPicker : IAttachmentPicker
{
    public List<string> Files { get; } = [];

    public IReadOnlyList<string> PickFiles(string title) => [.. Files];
}
