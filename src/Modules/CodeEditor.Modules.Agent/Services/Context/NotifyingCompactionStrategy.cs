using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>A compaction strategy that reports when it ran: for the feed line and to reset read files.</summary>
public sealed class NotifyingCompactionStrategy(CompactionStrategy inner, Action compacted) : CompactionStrategy(_ => true)
{
    protected override async ValueTask<bool> CompactCoreAsync(CompactionMessageIndex index, ILogger logger, CancellationToken cancellationToken)
    {
        var done = await inner.CompactAsync(index, logger, cancellationToken);
        if (done)
        {
            compacted();
        }

        return done;
    }
}
