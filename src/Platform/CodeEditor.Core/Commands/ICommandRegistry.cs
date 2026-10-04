using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CodeEditor.Core.Commands;

/// <summary>
/// Command registry. Modules register commands on activation and remove them via the returned
/// <see cref="IDisposable"/>.
/// </summary>
public interface ICommandRegistry
{
    event EventHandler? Changed;

    /// <summary>Snapshot of all commands; rebuilt only after a change.</summary>
    ImmutableArray<CommandDefinition> Commands { get; }

    /// <exception cref="InvalidOperationException">A command with this id is already registered.</exception>
    IDisposable Register(CommandDefinition command);

    bool TryGet(string id, [NotNullWhen(true)] out CommandDefinition? command);
}
