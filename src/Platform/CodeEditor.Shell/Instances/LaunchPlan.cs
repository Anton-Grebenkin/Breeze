namespace CodeEditor.Shell.Instances;

/// <summary>
/// Where a launch goes. With <see cref="Target"/> the request is sent to that window and this process exits; if the
/// window doesn't answer, this process opens <see cref="Folder"/> or <see cref="File"/> itself.
/// </summary>
public sealed record LaunchPlan
{
    public WindowEntry? Target { get; init; }

    public string? Folder { get; init; }

    public string? File { get; init; }

    /// <summary>No path and no other windows: the previous folder opens, as after a restart.</summary>
    public bool RestoreLastSession { get; init; }

    /// <summary>What the target window gets: the file to open or its own folder.</summary>
    public string? TargetPath => File ?? Folder;

    /// <summary>The same plan without a target: when the target window didn't answer.</summary>
    public LaunchPlan Here() => this with { Target = null };
}
