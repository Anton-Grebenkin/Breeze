using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Last compaction stage: compacts nothing, records the estimate of the history sent to the model so
/// <see cref="ContextMeter"/> can correct it by the real input from the response.
/// </summary>
internal sealed class MeteringCompactionStrategy(ContextMeter meter) : CompactionStrategy(CompactionTriggers.Always)
{
    protected override ValueTask<bool> CompactCoreAsync(CompactionMessageIndex index, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(index);
        meter.Sent(ContextEstimate.Of(index));
        return ValueTask.FromResult(false);
    }
}
