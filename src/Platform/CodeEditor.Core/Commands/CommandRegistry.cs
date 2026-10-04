using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using CodeEditor.Core.Common;

namespace CodeEditor.Core.Commands;

/// <summary>
/// Command registry with O(1) lookup by id. Not thread-safe: used from the UI thread.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> _commands = new(StringComparer.Ordinal);
    private ImmutableArray<CommandDefinition>? _snapshot;

    public event EventHandler? Changed;

    public ImmutableArray<CommandDefinition> Commands => _snapshot ??= [.. _commands.Values];

    public IDisposable Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_commands.TryAdd(command.Id, command))
        {
            throw new InvalidOperationException($"Command '{command.Id}' is already registered.");
        }

        OnChanged();
        return new DisposableAction(() => Unregister(command));
    }

    public bool TryGet(string id, [NotNullWhen(true)] out CommandDefinition? command) =>
        _commands.TryGetValue(id, out command);

    private void Unregister(CommandDefinition command)
    {
        // Remove only our own registration: the id may have been re-registered by another command.
        if (_commands.TryGetValue(command.Id, out var current) && ReferenceEquals(current, command))
        {
            _commands.Remove(command.Id);
            OnChanged();
        }
    }

    private void OnChanged()
    {
        _snapshot = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
