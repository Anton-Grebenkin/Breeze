namespace CodeEditor.Shell.Palette;

/// <summary>
/// Commands recently run from the palette, newest first. Persisted in the session state.
/// </summary>
public sealed class RecentCommands
{
    public const int Capacity = 20;

    private readonly List<string> _commandIds = [];

    public IReadOnlyList<string> Items => _commandIds;

    /// <summary>Restores the list from the previous session, newest first.</summary>
    public void Restore(IEnumerable<string> commandIds)
    {
        _commandIds.Clear();
        _commandIds.AddRange(commandIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(Capacity));
    }

    /// <summary>Moves the command to the top. O(<see cref="Capacity"/>).</summary>
    public void Add(string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        _commandIds.Remove(commandId);
        _commandIds.Insert(0, commandId);
        if (_commandIds.Count > Capacity)
        {
            _commandIds.RemoveAt(_commandIds.Count - 1);
        }
    }
}
