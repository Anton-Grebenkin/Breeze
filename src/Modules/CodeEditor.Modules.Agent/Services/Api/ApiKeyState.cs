using System.Globalization;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// API key of the service the conversation model belongs to (<see cref="ModelRoutes"/>) in the secret store, with a
/// change event: the chat updates its hint and the client is recreated. Switching services also counts as a key change.
/// </summary>
public sealed class ApiKeyState : IDisposable
{
    private readonly ISecretStore _secrets;
    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly IDisposable? _subscription;
    private string _secretName;

    public ApiKeyState(ISecretStore secrets, IOptionsMonitor<AgentOptions> options)
    {
        _secrets = secrets;
        _options = options;
        _secretName = ModelRoutes.For(options.CurrentValue).SecretName;
        _subscription = options.OnChange(OnOptionsChanged);
    }

    public event EventHandler? Changed;

    /// <summary>The key is there, or the connection needs none.</summary>
    public bool HasKey => Route.Keyless || _secrets.Get(SecretName) is not null;

    /// <summary>The current built-in service; <c>null</c> for a connection or an address of the user's own.</summary>
    public AgentService? Service => Route.Service;

    /// <summary>Where to get a key of the current service; <c>null</c> for a user's own endpoint.</summary>
    public Uri? KeyPage => Service?.KeyPage;

    /// <summary>Key dialog text: the current service by name; an own endpoint by host, with the protocol it needs.</summary>
    public string KeyPrompt()
    {
        if (Service is { } service)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyDialogPrompt, service.Title);
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyDialogPromptCustom, Route.ServiceTitle);
    }

    private ModelRoute Route => ModelRoutes.For(_options.CurrentValue);

    private string SecretName => Route.SecretName;

    public void Set(string key)
    {
        _secrets.Set(SecretName, key);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _secrets.Remove(SecretName);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A key was set or removed elsewhere (the connections panel): the chat checks again.</summary>
    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _subscription?.Dispose();

    private void OnOptionsChanged(AgentOptions options, string? name)
    {
        var secretName = ModelRoutes.For(options).SecretName;
        var changed = secretName != _secretName;
        _secretName = secretName;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
