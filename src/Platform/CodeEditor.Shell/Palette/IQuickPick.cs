namespace CodeEditor.Shell.Palette;

/// <summary>
/// Pick from a list in the palette, like <c>showQuickPick</c> in VS Code (agent models, saved chats). The palette opens
/// in the provider's mode without a prefix; <c>Esc</c> or a click outside cancels.
/// </summary>
public interface IQuickPick
{
    void Show(IQuickOpenProvider provider);
}
