namespace CodeEditor.Modules.Docker.ViewModels;

/// <summary>A panel action on Docker (<see cref="DockerActions"/>).</summary>
/// <param name="BusyText">Shown on the node while docker runs: "stopping…".</param>
/// <param name="Title">For the status bar: "Stopping api".</param>
internal sealed record DockerOperation(string BusyText, string Title, IReadOnlyList<string> Arguments, TimeSpan Timeout);
