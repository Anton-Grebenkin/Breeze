namespace CodeEditor.Modules.Docker.Commands;

/// <summary>
/// Docker panel command ids (ADR 0033). The argument is a tree node (context menu, row button); without one a command
/// takes the node selected on the visible panel, otherwise it asks to pick one in the palette.
/// </summary>
public static class DockerCommandIds
{
    public const string Refresh = "docker.refresh";
    public const string Start = "docker.start";
    public const string Stop = "docker.stop";
    public const string Restart = "docker.restart";
    public const string Remove = "docker.remove";
    public const string Logs = "docker.logs";
    public const string Inspect = "docker.inspect";
    public const string CopyId = "docker.copyId";

    /// <summary>Opens a published container port in the browser; the argument may also be the port itself.</summary>
    public const string OpenInBrowser = "docker.openInBrowser";
    public const string RemoveImage = "docker.removeImage";

    /// <summary>A compose file targets the whole project, a service only itself.</summary>
    public const string ComposeUp = "docker.composeUp";
    public const string ComposeUpBuild = "docker.composeUpBuild";
    public const string ComposeDown = "docker.composeDown";
    public const string ComposeRestart = "docker.composeRestart";
    public const string OpenComposeFile = "docker.openComposeFile";
}
