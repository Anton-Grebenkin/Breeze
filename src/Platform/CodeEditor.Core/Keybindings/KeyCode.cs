namespace CodeEditor.Core.Keybindings;

/// <summary>
/// Key without modifiers. Values match Win32 virtual-key codes, so the view converts a WPF key
/// with a single <c>KeyInterop.VirtualKeyFromKey</c> call.
/// </summary>
public enum KeyCode : byte
{
    None = 0,

    Backspace = 0x08,
    Tab = 0x09,
    Enter = 0x0D,
    Escape = 0x1B,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    Insert = 0x2D,
    Delete = 0x2E,

    D0 = 0x30, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    A = 0x41, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    NumPad0 = 0x60, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    Multiply = 0x6A,
    Add = 0x6B,
    Subtract = 0x6D,
    Decimal = 0x6E,
    Divide = 0x6F,

    F1 = 0x70, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

    Semicolon = 0xBA,
    EqualsSign = 0xBB,
    Comma = 0xBC,
    Minus = 0xBD,
    Period = 0xBE,
    Slash = 0xBF,
    Backquote = 0xC0,
    BracketLeft = 0xDB,
    Backslash = 0xDC,
    BracketRight = 0xDD,
    Quote = 0xDE,
}
