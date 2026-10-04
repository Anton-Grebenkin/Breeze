using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Sheet names by Excel rules: 1–31 characters, no : \ / ? * [ ], no apostrophe at either end, no duplicates.
/// </summary>
internal static class SheetNames
{
    public const int MaxLength = 31;
    private static readonly char[] Forbidden = [':', '\\', '/', '?', '*', '[', ']'];

    /// <exception cref="AgentToolException">The name is invalid or already taken.</exception>
    public static string Validate(string? name, IEnumerable<string> existing)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxLength || trimmed.IndexOfAny(Forbidden) >= 0 || trimmed.StartsWith('\'') || trimmed.EndsWith('\''))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.InvalidSheetName, name, MaxLength));
        }

        if (existing.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.SheetExists, trimmed));
        }

        return trimmed;
    }
}
