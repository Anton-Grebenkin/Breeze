namespace CodeEditor.Core.Keybindings;

[Flags]
public enum KeyModifiers : byte
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}
