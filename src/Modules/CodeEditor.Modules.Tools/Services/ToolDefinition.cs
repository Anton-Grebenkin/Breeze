namespace CodeEditor.Modules.Tools.Services;

/// <summary>
/// Tool from the shelf: a folder with <c>tool.md</c> (description and parameters in the header) and a script.
/// </summary>
/// <param name="Name">Folder name: the tool is called by it.</param>
/// <param name="Folder">Full path of the tool folder.</param>
/// <param name="Script">Full path of the script.</param>
/// <param name="IsPersonal">From the user data folder: the user's own tool, trusted without asking.</param>
public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters,
    string Folder,
    string Script,
    ToolScriptKind Kind,
    bool IsPersonal)
{
    public const string DefinitionFile = "tool.md";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>Parameters for lists: "path, pattern".</summary>
    public string Signature => string.Join(", ", Parameters.Select(parameter => parameter.Name));
}
