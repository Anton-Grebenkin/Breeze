namespace CodeEditor.Modules.Terminal.Services.Shells;

/// <summary>Where shells are installed: the system folder, Program Files, PATH and the command interpreter.</summary>
public sealed record ShellLocations(string SystemFolder, string ProgramFiles, string? Path, string? CommandInterpreter)
{
    public static ShellLocations FromSystem() => new(
        Environment.SystemDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetEnvironmentVariable("PATH"),
        Environment.GetEnvironmentVariable("ComSpec"));
}
