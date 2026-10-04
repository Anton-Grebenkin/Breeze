using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Wpf.Input;

/// <summary>
/// Single keyboard route: window <c>PreviewKeyDown</c> → <see cref="KeyChord"/> → <see cref="KeybindingResolver"/> →
/// <see cref="ICommandService"/>. Unbound keys go on to the focused element. The chord hint in the status bar
/// ("waiting for second key") clears on the next key, restoring the previous message unless a command changed it.
/// </summary>
public sealed class KeyboardRouter(
    KeybindingResolver resolver,
    ICommandService commands,
    IContextKeyService context,
    StatusBarViewModel statusBar)
{
    private string? _chordMessage;
    private string _messageBeforeChord = string.Empty;

    public void Attach(Window window)
    {
        window.PreviewKeyDown += OnPreviewKeyDown;
        window.Deactivated += OnDeactivated;
    }

    public void Detach(Window window)
    {
        window.PreviewKeyDown -= OnPreviewKeyDown;
        window.Deactivated -= OnDeactivated;
    }

    // async void is fine: this is an event handler and ExecuteAsync doesn't throw.
    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifier(key))
        {
            return;
        }

        var chord = new KeyChord(ToModifiers(e.KeyboardDevice.Modifiers), (KeyCode)KeyInterop.VirtualKeyFromKey(key));
        var resolution = resolver.Resolve(chord, context);

        switch (resolution.Kind)
        {
            case KeyResolutionKind.WaitingForSecondChord:
                e.Handled = true;
                ShowChordMessage(string.Format(CultureInfo.CurrentCulture, Strings.WaitingForSecondChord, chord));
                break;

            case KeyResolutionKind.ChordNotFound:
                e.Handled = true;
                ShowChordMessage(string.Format(CultureInfo.CurrentCulture, Strings.ChordNotAssigned, resolution.PendingChord, chord));
                break;

            case KeyResolutionKind.Command when resolution.Binding is { } binding:
                e.Handled = true;
                ClearChordMessage();
                await commands.ExecuteAsync(binding.CommandId, binding.Argument);
                break;

            default:
                ClearChordMessage();
                break;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        resolver.Reset();
        ClearChordMessage();
    }

    private void ShowChordMessage(string message)
    {
        if (_chordMessage is null)
        {
            _messageBeforeChord = statusBar.Message;
        }

        _chordMessage = message;
        statusBar.Message = message;
    }

    private void ClearChordMessage()
    {
        if (_chordMessage is null)
        {
            return;
        }

        if (statusBar.Message == _chordMessage)
        {
            statusBar.Message = _messageBeforeChord;
        }

        _chordMessage = null;
    }

    private static bool IsModifier(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;

    private static KeyModifiers ToModifiers(ModifierKeys modifiers)
    {
        var result = KeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= KeyModifiers.Ctrl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= KeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= KeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= KeyModifiers.Win;
        }

        return result;
    }
}
