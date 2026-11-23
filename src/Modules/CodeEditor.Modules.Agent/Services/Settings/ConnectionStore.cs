using System.Diagnostics.CodeAnalysis;
using System.Text;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// The user's connections (ADR 0055) for the editor's own interface: they are written to the user's settings, and each
/// key to the secret store, so nobody has to edit JSON to add a gateway.
/// </summary>
/// <param name="keyState">Told when a key changes, so the chat stops asking for one; <c>null</c> in tests.</param>
public sealed class ConnectionStore(IOptionsMonitor<AgentOptions> options, ISettingsService settings, ISecretStore secrets, ApiKeyState? keyState = null)
{
    public const string SettingKey = "agent.connections";

    private const string FallbackId = "connection";

    public IReadOnlyList<AgentConnection> Connections => options.CurrentValue.Connections;

    public AgentConnection? Find(string id) =>
        Connections.FirstOrDefault(connection => string.Equals(connection.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A free id for a new connection: from its name, or from the host when the name has no Latin letters or digits
    /// (a Cyrillic name at gateway.example becomes <c>gateway</c>); <c>-2</c>, <c>-3</c> when taken.
    /// </summary>
    public string NewId(string name, string endpoint)
    {
        var host = Uri.TryCreate(endpoint, UriKind.Absolute, out var address) ? address.Host.Split('.')[0] : string.Empty;
        var slug = Slug(name) is { Length: > 0 } fromName ? fromName : Slug(host) is { Length: > 0 } fromHost ? fromHost : FallbackId;
        var id = slug;
        for (var number = 2; Find(id) is not null; number++)
        {
            id = $"{slug}-{number}";
        }

        return id;
    }

    /// <summary>Adds the connection, or replaces the one with the same id.</summary>
    public bool TrySave(AgentConnection connection, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var replaced = false;
        List<AgentConnection> connections = [];
        foreach (var existing in Connections)
        {
            var same = string.Equals(existing.Id, connection.Id, StringComparison.OrdinalIgnoreCase);
            connections.Add(same ? connection : existing);
            replaced |= same;
        }

        if (!replaced)
        {
            connections.Add(connection);
        }

        return Write(connections, out error);
    }

    /// <summary>Removes the connection and its key.</summary>
    public bool TryRemove(string id, [NotNullWhen(false)] out string? error)
    {
        if (!Write([.. Connections.Where(connection => !string.Equals(connection.Id, id, StringComparison.OrdinalIgnoreCase))], out error))
        {
            return false;
        }

        secrets.Remove(AgentSecrets.ForConnection(id));
        keyState?.Refresh();
        return true;
    }

    public bool HasKey(AgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return secrets.Get(AgentSecrets.ForConnection(connection.Id)) is not null;
    }

    public string? Key(AgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return secrets.Get(AgentSecrets.ForConnection(connection.Id));
    }

    public void SetKey(string id, string key)
    {
        secrets.Set(AgentSecrets.ForConnection(id), key);
        keyState?.Refresh();
    }

    private bool Write(List<AgentConnection> connections, [NotNullWhen(false)] out string? error) =>
        settings.TrySetUserValue(SettingKey, connections.Count == 0 ? null : ConnectionsJson.ToNode(connections), out error);

    // Latin letters and digits, the rest becomes a single dash.
    private static string Slug(string text)
    {
        var slug = new StringBuilder(text.Length);
        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().Trim('-');
    }
}
