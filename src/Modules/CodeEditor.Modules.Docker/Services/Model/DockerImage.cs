using CodeEditor.Modules.Docker.Services.Cli;

namespace CodeEditor.Modules.Docker.Services.Model;

/// <summary>An image from <c>docker images</c> (<see cref="DockerQueries.Images"/>).</summary>
/// <param name="Id">Short ID, as in docker output.</param>
/// <param name="Size">Size as docker writes it: "113MB", "9.73MB".</param>
public sealed record DockerImage(string Id, string Repository, string Tag, string Size, DateTimeOffset? Created)
{
    private const string None = "<none>";

    /// <summary>An unnamed image: left over from a build or untagged by an update.</summary>
    public bool IsDangling => Repository == None || Tag == None;

    /// <summary>"postgres:17"; "&lt;none&gt;" for an unnamed image.</summary>
    public string Title => IsDangling ? None : Repository + ":" + Tag;

    /// <summary>How to name the image for docker: name:tag (removal untags) or the ID of an unnamed image.</summary>
    public string Reference => IsDangling ? Id : Title;
}
