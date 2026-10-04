using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Services.Conversation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// <c>--show-prompt folder</c>: prints as JSON the system prompt and tools (name, description, parameter schema) that
/// the first of <c>--models</c> would get in the first of <c>--modes</c> for that folder. No network and no writes:
/// the folder is only opened as the workspace.
/// </summary>
internal static class PromptDump
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static async Task WriteAsync(EvalOptions options, string folder)
    {
        var model = options.Models[0];
        var mode = options.Modes[0];
        var userData = Path.Combine(Path.GetTempPath(), "CodeEditor.Eval", "show-prompt");
        EvalHost.WriteSettings(userData, model, mode, options);

        using var dispatcher = new EvalDispatcher();
        await using var services = EvalHost.Build(userData, dispatcher, "show-prompt", dryRun: true);
        await dispatcher.InvokeAsync(() => services.GetRequiredService<IWorkspace>().Open(folder));

        var preview = services.GetRequiredService<AgentConversation>().Preview();
        var dump = new
        {
            Model = model,
            Mode = mode,
            Folder = Path.GetFullPath(folder),
            preview.Instructions,
            Tools = preview.Tools.OfType<AIFunction>().Select(tool => new { tool.Name, tool.Description, Parameters = tool.JsonSchema }),
        };
        Console.WriteLine(JsonSerializer.Serialize(dump, JsonOptions));
    }
}
