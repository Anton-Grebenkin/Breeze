namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Chat start blocks (ADR 0012): <c>&lt;workspace&gt;</c> with a folder snapshot and <c>&lt;memory&gt;</c> with the memory
/// note index. They go into the first chat message, which never changes and stays cached. Compaction folds that message
/// into the summary, so the same blocks are appended to it (<see cref="ContextCompaction.HarnessState"/>). Before the
/// first message, memory forgets helper-model notes that have not been read for long (ADR 0029).
/// </summary>
public sealed class ChatStartContext(WorkspaceSnapshot snapshot, ProjectMemory memory)
{
    /// <summary>Empty memory is information too: the model will not try to read it.</summary>
    public const string EmptyMemory = "(no notes yet)";

    private string? _lastSnapshot;

    /// <summary>Blocks for the first chat message; the snapshot is kept for the summary.</summary>
    public async Task<IReadOnlyList<string>> BuildAsync(CancellationToken cancellationToken)
    {
        _lastSnapshot = await snapshot.BuildAsync(cancellationToken);
        memory.ForgetUnused();
        return Blocks();
    }

    /// <summary>The same blocks without a new snapshot, for a mid-turn summary.</summary>
    public IReadOnlyList<string> Blocks()
    {
        if (memory.Folder is null)
        {
            return [];
        }

        var blocks = new List<string>();
        if (_lastSnapshot is { Length: > 0 } workspace)
        {
            blocks.Add($"<workspace>\n{workspace}\n</workspace>");
        }

        blocks.Add($"<memory>\n{memory.IndexForModel() ?? EmptyMemory}\n</memory>");
        return blocks;
    }
}
