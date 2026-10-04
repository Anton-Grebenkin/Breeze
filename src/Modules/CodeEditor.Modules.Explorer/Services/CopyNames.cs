using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Explorer.Resources;

namespace CodeEditor.Modules.Explorer.Services;

/// <summary>
/// Free name for a copy in the same folder, as in VS Code: "name copy.ext", then "name copy 2.ext" and so on. A folder or
/// a dotfile (".gitignore") keeps its whole name as the stem.
/// </summary>
internal static class CopyNames
{
    public static string NextFree(IFileSystem fileSystem, string folder, string name, bool isDirectory)
    {
        var extension = isDirectory ? string.Empty : Path.GetExtension(name);
        var stem = name[..^extension.Length];
        if (stem.Length == 0)
        {
            (stem, extension) = (name, string.Empty);
        }

        for (var number = 1; ; number++)
        {
            var candidate = Path.Combine(folder, Numbered(stem, number) + extension);
            if (!fileSystem.FileExists(candidate) && !fileSystem.DirectoryExists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Numbered(string stem, int number) => number == 1
        ? string.Format(CultureInfo.CurrentCulture, Strings.CopyName, stem)
        : string.Format(CultureInfo.CurrentCulture, Strings.CopyNameNumbered, stem, number);
}
