using CodeEditor.Core.Commands;

namespace CodeEditor.Shell.Palette;

/// <summary>A command available in the palette; collected once on open, then only filtered.</summary>
public readonly record struct PaletteCandidate(CommandDefinition Command, string? Shortcut);
