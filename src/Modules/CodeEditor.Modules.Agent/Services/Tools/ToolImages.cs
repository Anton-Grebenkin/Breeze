using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// Images that tools showed the model in this turn step: <c>view_image</c>, a browser snapshot (ADR 0030). Showing one
/// ends the tool loop after the current calls, and the turn's checks node (<see cref="Workflow.ChecksExecutor"/>) sends
/// the images to the model in the next message, like a user attachment (ADR 0020): every protocol accepts images in a
/// message, not all accept them in a tool result. Written by tool threads, read by the turn graph.
/// </summary>
public sealed class ToolImages(IOptionsMonitor<AgentOptions> options) : IAgentImages
{
    private readonly Lock _gate = new();
    private readonly List<AIContent> _pending = [];

    public bool CanShow => ModelProfiles.For(options.CurrentValue.Model).SeesImages;

    public bool TryShow(string name, byte[] data, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > AttachmentReader.MaxImageBytes)
        {
            return false;
        }

        lock (_gate)
        {
            _pending.Add(new TextContent($"<image source=\"{name.Replace('"', '\'')}\" />"));
            _pending.Add(new DataContent(data, mediaType) { Name = name });
        }

        if (FunctionInvokingChatClient.CurrentContext is { } invocation)
        {
            invocation.Terminate = true;
        }

        return true;
    }

    /// <summary>A message with the shown images; <c>null</c> if there are none.</summary>
    public ChatMessage? Take()
    {
        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                return null;
            }

            var message = new ChatMessage(ChatRole.User, [new TextContent(PromptSections.EditorNote(Strings.ImagesNote)), .. _pending]);
            _pending.Clear();
            return message;
        }
    }

    /// <summary>New turn: images of a stopped turn do not belong to it.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _pending.Clear();
        }
    }
}
