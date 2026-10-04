using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Memory;

/// <summary>
/// Folder memory (ADR 0012, 0029): notes in <c>.breeze/agent/memory/</c>, one file per note (<see cref="MemoryNoteFile"/>),
/// and a <c>MEMORY.md</c> index built from them. The note list goes into the first chat message; the agent reads a note
/// by name, and reading counts as use. Helper-model notes unread for long are forgotten. The prompt decides what is worth
/// keeping; this class validates name, type and size. Saves are serialized: the index is rebuilt under a lock.
/// </summary>
public sealed partial class ProjectMemory(IWorkspace workspace, IFileSystem fileSystem, TimeProvider time)
{
    public const string FolderName = "memory";
    public const string IndexFileName = "MEMORY.md";
    public const int MaxNoteCharacters = 4_000;
    public const int MaxDescriptionCharacters = 200;
    public const int MaxNotes = 100;

    /// <summary>
    /// An older note is listed as "may be outdated"; a helper-model note the agent has not read for this long is forgotten.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    private static readonly string[] TypeOrder = ["user", "feedback", "project", "reference"];

    private static readonly FrozenSet<string> TypeSet = TypeOrder.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Note types in list order.</summary>
    public static IReadOnlyList<string> Types => TypeOrder;

    private readonly Lock _gate = new();

    public string? Folder => AgentDataFolder.PathOf(workspace, FolderName);

    /// <summary>All folder notes, by type and name; corrupt files are skipped.</summary>
    public IReadOnlyList<MemoryNote> Notes()
    {
        if (Folder is not { } folder || !fileSystem.DirectoryExists(folder))
        {
            return [];
        }

        return [.. fileSystem.EnumerateEntries(folder)
            .Where(static entry => !entry.IsDirectory && entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && entry.Name != IndexFileName)
            .Select(entry => MemoryNoteFile.Parse(fileSystem.ReadAllText(entry.FullPath)))
            .OfType<MemoryNote>()
            .OrderBy(static note => IndexOf(note.Type))
            .ThenBy(static note => note.Name, StringComparer.Ordinal)];
    }

    /// <summary>The note list for the model: name, type, date, staleness mark, description; <c>null</c> if there are no notes.</summary>
    public string? IndexForModel()
    {
        var today = Today();
        var notes = Notes();
        return notes.Count == 0 ? null : string.Join('\n', notes.Select(note =>
            $"- {note.Name} ({note.Type}, updated {MemoryNoteFile.Date(note.Updated)}{(IsStale(note.Updated, today) ? ", may be outdated" : string.Empty)}): {note.Description}"));
    }

    /// <summary>The note as is, without counting a use.</summary>
    public MemoryNote? Read(string name) =>
        Folder is { } folder && IsValidName(name) && fileSystem.FileExists(PathOf(folder, name)) ? MemoryNoteFile.Parse(fileSystem.ReadAllText(PathOf(folder, name))) : null;

    /// <summary>The note for the agent: the read counts as a use, so memory will not forget the note.</summary>
    public MemoryNote? Use(string name)
    {
        lock (_gate)
        {
            if (Folder is not { } folder || Read(name) is not { } note)
            {
                return null;
            }

            var used = note with { Used = Today(), Uses = note.Uses + 1 };
            try
            {
                fileSystem.WriteAllBytesAtomic(PathOf(folder, name), Encoding.UTF8.GetBytes(MemoryNoteFile.Render(used)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Use tracking is secondary: the agent gets the note anyway.
            }

            return used;
        }
    }

    /// <returns><c>true</c> if the note is new, <c>false</c> if updated.</returns>
    /// <param name="automatic">Proposed by the helper model after a turn; a save by the agent clears this mark for good.</param>
    /// <exception cref="AgentToolException">No folder; invalid name, type or size; memory is full.</exception>
    public bool Save(string name, string type, string description, string content, bool automatic = false)
    {
        var folder = RequireFolder();
        Validate(name, type, content);
        lock (_gate)
        {
            var isNew = !fileSystem.FileExists(PathOf(folder, name));
            if (isNew && Notes().Count >= MaxNotes)
            {
                throw new AgentToolException(Format(Strings.MemoryFull, MaxNotes));
            }

            var previous = isNew ? null : Read(name);
            var today = Today();
            var note = new MemoryNote(name, type, OneLine(description), today, content.Trim())
            {
                IsAutomatic = automatic && previous?.IsAutomatic != false,
                Used = today,
                Uses = previous?.Uses ?? 0,
            };
            Write(folder, () => fileSystem.WriteAllBytesAtomic(PathOf(folder, name), Encoding.UTF8.GetBytes(MemoryNoteFile.Render(note))));
            return isNew;
        }
    }

    /// <returns><c>false</c> if there is no such note.</returns>
    public bool Delete(string name)
    {
        var folder = RequireFolder();
        lock (_gate)
        {
            if (!IsValidName(name) || !fileSystem.FileExists(PathOf(folder, name)))
            {
                return false;
            }

            Write(folder, () => fileSystem.DeleteFile(PathOf(folder, name)));
            return true;
        }
    }

    /// <summary>
    /// Forgets helper-model notes the agent has not read for longer than <see cref="StaleAfter"/>; they go to the recycle
    /// bin rather than being erased. Agent and user notes are never forgotten, only marked "may be outdated".
    /// </summary>
    /// <returns>Names of forgotten notes; empty if there is nothing to forget or the folder is unavailable.</returns>
    public IReadOnlyList<string> ForgetUnused()
    {
        if (Folder is not { } folder)
        {
            return [];
        }

        lock (_gate)
        {
            var today = Today();
            var unused = Notes().Where(note => note.IsAutomatic && IsStale(note.Used, today)).Select(static note => note.Name).ToList();
            if (unused.Count == 0)
            {
                return [];
            }

            try
            {
                Write(folder, () => unused.ForEach(name => fileSystem.DeleteToRecycleBin(PathOf(folder, name))));
                return unused;
            }
            catch (AgentToolException)
            {
                return [];
            }
        }
    }

    /// <summary>The index for the user (Folder Memory command); created with a header if missing.</summary>
    /// <returns>The index path; <c>null</c> if no folder is open.</returns>
    public string? EnsureIndexFile()
    {
        if (Folder is not { } folder)
        {
            return null;
        }

        lock (_gate)
        {
            Write(folder, () => { });
            return Path.Combine(folder, IndexFileName);
        }
    }

    public static bool IsValidName(string name) => name is not null && NamePattern().IsMatch(name);

    // The change and the index rebuild form one operation, so the list always matches the notes.
    private void Write(string folder, Action change)
    {
        try
        {
            AgentDataFolder.Ensure(workspace, fileSystem, folder);
            change();
            fileSystem.WriteAllBytesAtomic(Path.Combine(folder, IndexFileName), Encoding.UTF8.GetBytes(IndexForUser()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentToolException(exception.Message);
        }
    }

    private string IndexForUser()
    {
        var text = new StringBuilder(Strings.MemoryIndexHeader).Append("\n\n");
        foreach (var note in Notes())
        {
            text.Append(CultureInfo.InvariantCulture, $"- [{note.Name}]({note.Name}.md) — {note.Description} ({note.Type}, {MemoryNoteFile.Date(note.Updated)})\n");
        }

        return text.ToString();
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    private string RequireFolder() => Folder ?? throw new AgentToolException(Strings.MemoryNoFolder);

    private static void Validate(string name, string type, string content)
    {
        if (!IsValidName(name))
        {
            throw new AgentToolException(Strings.MemoryBadName);
        }

        if (!TypeSet.Contains(type))
        {
            throw new AgentToolException(Strings.MemoryBadType);
        }

        if (content.Length > MaxNoteCharacters)
        {
            throw new AgentToolException(Format(Strings.MemoryTooLong, MaxNoteCharacters));
        }
    }

    private static bool IsStale(DateOnly date, DateOnly today) => today.DayNumber - date.DayNumber > StaleAfter.TotalDays;

    private static int IndexOf(string type) => Array.IndexOf(TypeOrder, type) is var index and >= 0 ? index : TypeOrder.Length;

    private static string OneLine(string text)
    {
        var line = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length <= MaxDescriptionCharacters ? line : line[..MaxDescriptionCharacters];
    }

    private static string PathOf(string folder, string name) => Path.Combine(folder, name + ".md");

    private static string Format(string format, object value) => string.Format(CultureInfo.CurrentCulture, format, value);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
