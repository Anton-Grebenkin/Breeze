using System.Globalization;

namespace CodeEditor.Modules.Agent.Services.Memory;

/// <summary>
/// A memory note file (<see cref="ProjectMemory"/>): a "---" header with name, type, description and updated fields,
/// plus <c>source: auto</c> for helper-model notes; <c>used</c> and <c>uses</c> say when and how often the agent read it.
/// Then the text. Fields missing in older notes get defaults.
/// </summary>
public static class MemoryNoteFile
{
    public const string DateFormat = "yyyy-MM-dd";

    private const string Fence = "---";
    private const string AutomaticSource = "auto";

    public static string Date(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string Render(MemoryNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var source = note.IsAutomatic ? $"source: {AutomaticSource}\n" : string.Empty;
        var uses = note.Uses.ToString(CultureInfo.InvariantCulture);
        return $"{Fence}\nname: {note.Name}\ntype: {note.Type}\ndescription: {note.Description}\nupdated: {Date(note.Updated)}\n{source}used: {Date(note.Used)}\nuses: {uses}\n{Fence}\n\n{note.Content}\n";
    }

    /// <summary>A "---" header of "key: value" fields, then text; without a header or a name it is not a note.</summary>
    public static MemoryNote? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != Fence)
        {
            return null;
        }

        var end = Array.FindIndex(lines, 1, static line => line.Trim() == Fence);
        if (end < 0)
        {
            return null;
        }

        var fields = lines[1..end]
            .Select(static line => line.Split(':', 2))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(static parts => parts[0].Trim(), static parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue("name", out var name) || !ProjectMemory.IsValidName(name))
        {
            return null;
        }

        var updated = DateField(fields, "updated") ?? DateOnly.MinValue;
        return new MemoryNote(name, fields.GetValueOrDefault("type", "project"), fields.GetValueOrDefault("description", string.Empty), updated, string.Join('\n', lines[(end + 1)..]).Trim())
        {
            IsAutomatic = fields.GetValueOrDefault("source") == AutomaticSource,
            Used = DateField(fields, "used") ?? updated,
            Uses = fields.TryGetValue("uses", out var uses) && int.TryParse(uses, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0,
        };
    }

    private static DateOnly? DateField(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
