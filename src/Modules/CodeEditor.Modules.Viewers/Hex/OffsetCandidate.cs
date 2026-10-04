namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>One reading of the entered offset: as a hexadecimal or a decimal number.</summary>
public readonly record struct OffsetCandidate(long Offset, bool IsHexadecimal);
