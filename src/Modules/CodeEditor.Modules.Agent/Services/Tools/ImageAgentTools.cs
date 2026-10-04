using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// The <c>view_image</c> tool (ADR 0030): the agent looks at a workspace image (screenshot, mockup, diagram). The image
/// is not part of the tool result: <see cref="ToolImages"/> shows it and the model gets it in the next message.
/// Read-only, so available in every mode; hidden from models that cannot see images (<see cref="ModelProfile.SeesImages"/>).
/// </summary>
public sealed class ImageAgentTools(IWorkspace workspace, IFileSystem fileSystem, ToolImages images) : IAgentToolProvider
{
    public const string ViewImageName = "view_image";

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(View, ViewImageName,
            "Shows you an image file of the workspace: a screenshot, a mockup, a diagram (png, jpg, gif or webp up to 5 MB). " +
            "The image arrives in the next message. Use it when the task refers to a picture; read text files with the read tools.")),
    ];

    private string View([Description("The image file relative to the workspace root, like 'docs/mockup.png'.")] string path)
    {
        var full = WorkspacePaths.Resolve(workspace, path);
        var name = workspace.RelativePath(full).Replace('\\', '/');
        if (AttachmentReader.ImageType(full) is not { } mediaType)
        {
            throw new AgentToolException(Format(Strings.ViewImageUnsupported, name));
        }

        if (!fileSystem.FileExists(full))
        {
            throw new AgentToolException(Format(Strings.FileNotFound, name));
        }

        if (fileSystem.GetFileLength(full) > AttachmentReader.MaxImageBytes)
        {
            throw new AgentToolException(Format(Strings.ViewImageTooLarge, name));
        }

        return images.TryShow(name, fileSystem.ReadAllBytes(full), mediaType)
            ? Format(Strings.ViewImageResult, name)
            : throw new AgentToolException(Format(Strings.ViewImageTooLarge, name));
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
