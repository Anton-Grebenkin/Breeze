using CodeEditor.Shell.Services;

namespace CodeEditor.Testing;

/// <summary>
/// Dialogs with preset answers; records what was asked.
/// </summary>
public sealed class FakeDialogs : IDialogService
{
    public bool ConfirmAnswer { get; set; } = true;

    public SaveChoice SaveAnswer { get; set; } = SaveChoice.Save;

    public List<string> Confirmations { get; } = [];

    public List<IReadOnlyList<string>> SaveQuestions { get; } = [];

    /// <summary>Answer to a secret prompt; <c>null</c> means Cancel.</summary>
    public string? SecretAnswer { get; set; }

    public List<string> SecretPrompts { get; } = [];

    public bool Confirm(string message, string detail, string confirmText)
    {
        Confirmations.Add(message);
        return ConfirmAnswer;
    }

    /// <summary>Messages and details of information dialogs.</summary>
    public List<(string Message, string Detail)> Information { get; } = [];

    public void Inform(string message, string detail) => Information.Add((message, detail));

    public SaveChoice AskToSave(IReadOnlyList<string> fileNames)
    {
        SaveQuestions.Add(fileNames);
        return SaveAnswer;
    }

    public string? PromptSecret(string title, string message, Uri? link = null)
    {
        SecretPrompts.Add(title);
        return SecretAnswer;
    }
}
