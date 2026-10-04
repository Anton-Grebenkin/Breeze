using CodeEditor.Core.Files;

namespace CodeEditor.Core.Processes;

/// <summary>
/// Finds a program on PATH without starting a process: PATH folders × PATHEXT extensions. Modules use it to decide
/// whether to show a tool that is not installed, and the folder snapshot tells the model which programs exist.
/// </summary>
public static class ExecutablePaths
{
    private const string DefaultExtensions = ".COM;.EXE;.BAT;.CMD";

    /// <summary>Full paths of the program in PATH search order; empty when it is not found.</summary>
    /// <remarks>O(folders × extensions) file checks, evaluated lazily: <c>Any()</c> stops at the first hit.</remarks>
    /// <param name="path">Folders to search; <c>null</c> means the PATH variable.</param>
    /// <param name="extensions">Executable extensions; <c>null</c> means the PATHEXT variable.</param>
    public static IEnumerable<string> Find(IFileSystem fileSystem, string name, string? path = null, string? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var suffixes = Split(extensions ?? Environment.GetEnvironmentVariable("PATHEXT") ?? DefaultExtensions);
        return Split(path ?? Environment.GetEnvironmentVariable("PATH"))
            .SelectMany(folder => suffixes.Select(suffix => Path.Combine(folder, name + suffix.ToLowerInvariant())))
            .Where(file => Exists(fileSystem, file));
    }

    // A PATH folder with invalid characters or no access is skipped, as Windows does.
    private static bool Exists(IFileSystem fileSystem, string file)
    {
        try
        {
            return fileSystem.FileExists(file);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static List<string> Split(string? value) =>
        [.. (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item => item.Trim('"')).Where(item => item.Length > 0)];
}
