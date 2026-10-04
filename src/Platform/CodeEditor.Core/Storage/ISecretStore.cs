namespace CodeEditor.Core.Storage;

/// <summary>
/// Secrets (API keys) kept outside settings files: <c>settings.json</c> may be shown or committed, a key must not.
/// </summary>
public interface ISecretStore
{
    string? Get(string name);

    void Set(string name, string value);

    void Remove(string name);
}
