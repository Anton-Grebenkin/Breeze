namespace CodeEditor.Shell.Services;

/// <summary>
/// System dialogs, used only where an action is hard to undo: deletion, losing edits.
/// </summary>
public interface IDialogService
{
    /// <summary>Returns <c>true</c> if the user confirmed with the <paramref name="confirmText"/> button.</summary>
    bool Confirm(string message, string detail, string confirmText);

    /// <summary>Information with an OK button, such as "About".</summary>
    void Inform(string message, string detail);

    /// <summary>"Save changes to …?" before closing files with edits.</summary>
    SaveChoice AskToSave(IReadOnlyList<string> fileNames);

    /// <summary>Masked input for a secret (API key). Returns <c>null</c> if the user cancelled.</summary>
    /// <param name="link">Where to get the secret, shown as a link under the message; <c>null</c> — no link.</param>
    string? PromptSecret(string title, string message, Uri? link = null);
}
