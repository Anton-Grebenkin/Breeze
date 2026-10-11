namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>A process holding a build output file (<see cref="LockedFiles"/>).</summary>
public readonly record struct LockingProcess(string Name, int Id);
