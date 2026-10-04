using FlaUI.Core.WindowsAPI;
using FlaUIKeyboard = FlaUI.Core.Input.Keyboard;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>Guarded FlaUI keyboard: keys are sent only while the app window is in the foreground (<see cref="InputGuard"/>).</summary>
public static class Keyboard
{
    public static void Type(string text)
    {
        InputGuard.EnsureKeyboardTarget();
        FlaUIKeyboard.Type(text);
    }

    public static void Type(params VirtualKeyShort[] keys)
    {
        InputGuard.EnsureKeyboardTarget();
        FlaUIKeyboard.Type(keys);
    }

    public static void TypeSimultaneously(params VirtualKeyShort[] keys)
    {
        InputGuard.EnsureKeyboardTarget();
        FlaUIKeyboard.TypeSimultaneously(keys);
    }
}
