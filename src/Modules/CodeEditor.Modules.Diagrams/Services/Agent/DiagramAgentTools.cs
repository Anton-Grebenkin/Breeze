using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Diagrams.Services.Agent;

/// <summary>
/// The <c>diagram</c> agent tool for Mermaid diagrams (ADR 0035). The agent writes a diagram with the regular edit
/// tools (into <c>.mmd</c> or a <c>```mermaid</c> block) and uses this tool to check it (<c>check</c>: errors with file
/// lines), look at it (<c>view</c>: the image arrives in the next message via <see cref="IAgentImages"/> if the model
/// sees images) and save it as an image alongside (<c>render</c>: SVG or PNG). Checking and viewing need no approval;
/// saving needs none unless it overwrites a foreign file (<see cref="DiagramApprovals"/>). Without a renderer
/// (windowless benchmark) there is no tool.
/// </summary>
public sealed class DiagramAgentTools(
    IEnumerable<IDiagramRenderer> renderers,
    DiagramToolInputs inputs,
    DiagramRenderTargets targets,
    IFileSystem fileSystem,
    IWorkspace workspace,
    DiagramWrites writes,
    IAgentImages images) : IAgentToolProvider, IAgentChangePreviewer
{
    public const string ToolName = "diagram";

    public const string Check = "check";
    public const string View = "view";
    public const string Render = "render";

    /// <summary>Maximum number of diagrams shown to the model per call.</summary>
    public const int MaxViewed = 4;

    public static IReadOnlyList<string> Actions { get; } = [Check, View, Render];

    private readonly IDiagramRenderer? _renderer = renderers.FirstOrDefault();

    public IEnumerable<AITool> CreateTools() => _renderer is null ? [] :
    [
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(UseAsync, ToolName,
            "Mermaid diagrams, the default diagram format of this editor: flowchart, sequence, class, state, ER, Gantt, mindmap, timeline and more. " +
            "Draw one when a picture explains better than text: architecture and modules, data flow, call sequence, states, a database schema, a plan. " +
            "Write the diagram with the edit tools into a .mmd file or a ```mermaid block of a Markdown file; the user sees it live in the diagram preview (Ctrl+K V). " +
            "Actions: check (validate a file or text: errors come with file line numbers), " +
            "view (render the diagram and look at it: the image arrives in the next message; check the layout and labels after drawing), " +
            "render (save the diagram as an image next to the file: arch.mmd -> arch.svg, README.md -> README-1.svg; only when an image file is needed). " +
            "After editing a diagram, check it before you finish.")),
    ];

    public bool CanPreview(string toolName) => toolName == ToolName;

    public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var files = string.Join('\n', targets.Foreign(arguments).Select(workspace.RelativePath));
        return Task.FromResult<IReadOnlyList<FileChangePreview>>(
            [new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, files) { Title = Strings.ToolOverwriteTitle, Header = Strings.ToolOverwriteHeader }]);
    }

    private async Task<string> UseAsync(
        [Description("check, view or render.")] string action,
        [Description("The diagram file relative to the workspace root: .mmd, .mermaid or Markdown with ```mermaid blocks.")] string? path = null,
        [Description("check, view: Mermaid text to try instead of a file.")] string? text = null,
        [Description("Markdown: the number of the ```mermaid block, from 1; default: all blocks.")] int? block = null,
        [Description("render: svg (default) or png.")] string? format = null,
        CancellationToken cancellationToken = default)
    {
        var renderer = _renderer ?? throw new AgentToolException(Strings.ToolUnavailable);
        try
        {
            return action switch
            {
                Check => await CheckAsync(renderer, await inputs.ResolveAsync(path, text, block), cancellationToken),
                View => await ViewAsync(renderer, path, text, block, cancellationToken),
                Render => await RenderAsync(renderer, path, block, format, cancellationToken),
                _ => throw new AgentToolException(Format(Strings.ToolUnknownAction, action, string.Join(", ", Actions))),
            };
        }
        catch (DiagramRendererException exception)
        {
            throw new AgentToolException(exception.Message);
        }
    }

    private static async Task<string> CheckAsync(IDiagramRenderer renderer, DiagramToolInput input, CancellationToken cancellationToken)
    {
        var types = new List<string>();
        var problems = new List<DiagramProblem>();
        foreach (var diagram in input.Diagrams)
        {
            var result = await renderer.CheckAsync(diagram.Text, cancellationToken);
            if (result.IsSuccess)
            {
                types.Add(result.Value);
            }
            else
            {
                problems.Add(DiagramErrors.Locate(diagram, result.Error!));
            }
        }

        ThrowIfAny(input, problems);
        return Format(Strings.ToolCheckPassed, input.Name, string.Join(", ", types));
    }

    // The model gets the image in the next message; diagrams with errors are not shown until they are fixed.
    private async Task<string> ViewAsync(IDiagramRenderer renderer, string? path, string? text, int? block, CancellationToken cancellationToken)
    {
        if (!images.CanShow)
        {
            return Strings.ToolViewNotSeen;
        }

        var input = await inputs.ResolveAsync(path, text, block);
        var shown = new List<(string Name, byte[] Png)>();
        var problems = new List<DiagramProblem>();
        foreach (var diagram in input.Diagrams.Take(MaxViewed))
        {
            var result = await renderer.RenderPngAsync(diagram.Text, DiagramTheme.Light, DiagramPngSize.Model, cancellationToken);
            if (result.IsSuccess)
            {
                shown.Add((input.ImageName(diagram), result.Value));
            }
            else
            {
                problems.Add(DiagramErrors.Locate(diagram, result.Error!));
            }
        }

        ThrowIfAny(input, problems);

        // Show every image within the model limit; the model is told which diagrams are missing and why.
        var notShown = shown.Where(image => !images.TryShow(image.Name, image.Png, "image/png")).Select(image => image.Name).ToList();
        if (notShown.Count == shown.Count)
        {
            throw new AgentToolException(Strings.ToolViewTooLarge);
        }

        var answer = Format(Strings.ToolViewResult, input.Name);
        if (notShown.Count > 0)
        {
            answer += " " + Format(Strings.ToolViewNotShown, string.Join(", ", notShown));
        }

        return input.Diagrams.Count > MaxViewed ? answer + " " + Format(Strings.ToolViewMore, MaxViewed) : answer;
    }

    private async Task<string> RenderAsync(IDiagramRenderer renderer, string? path, int? block, string? format, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AgentToolException(Strings.ToolRenderNeedsPath);
        }

        var kind = DiagramToolFormats.TryParse(format) ?? throw new AgentToolException(Format(Strings.ToolUnknownFormat, format ?? string.Empty));
        var input = await inputs.ResolveAsync(path, text: null, block);
        foreach (var target in input.Diagrams.Select(diagram => DiagramExportPaths.For(input.FullPath!, kind, diagram.Index)))
        {
            SensitivePaths.EnsureWritable(workspace.RelativePath(target));
        }

        DiagramExport export;
        try
        {
            export = await new DiagramExporter(renderer, fileSystem, writes).ExportAsync(input.FullPath!, input.Diagrams, kind, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentToolException(Format(Strings.ExportFailed, exception.Message));
        }

        var saved = Format(Strings.ToolRenderSaved, string.Join(", ", export.Written.Select(workspace.RelativePath)));
        return export.Problems.Count == 0
            ? saved
            : throw new AgentToolException((export.Written.Count > 0 ? saved + "\n" : string.Empty) + Problems(input, export.Problems));
    }

    private static void ThrowIfAny(DiagramToolInput input, List<DiagramProblem> problems)
    {
        if (problems.Count > 0)
        {
            throw new AgentToolException(Problems(input, problems));
        }
    }

    // Newlines live in code, not in resources: there they depend on how git checked out the .resx (LF or CRLF).
    private static string Problems(DiagramToolInput input, IReadOnlyList<DiagramProblem> problems) =>
        Format(Strings.ToolDiagramErrors, input.Name) + "\n" + string.Join("\n\n", problems.Select(DiagramErrors.Describe));

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
