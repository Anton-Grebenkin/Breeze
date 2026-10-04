using System.Globalization;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// API key of the current service (<see cref="AgentServices"/>) in the secret store, with a change event: the chat
/// updates its hint and the client is recreated. Switching services also counts as a key change.
/// </summary>
public sealed class ApiKeyState : IDisposable
{
    private readonly ISecretStore _secrets;
    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly IDisposable? _subscription;
    private string _endpoint;

    public ApiKeyState(ISecretStore secrets, IOptionsMonitor<AgentOptions> options)
    {
        _secrets = secrets;
        _options = options;
        _endpoint = options.CurrentValue.Endpoint;
        _subscription = options.OnChange(OnOptionsChanged);
    }

    public event EventHandler? Changed;

    public bool HasKey => _secrets.Get(SecretName) is not null;

    /// <summary>The current service; <c>null</c> for a user's own endpoint.</summary>
    public AgentService? Service => AgentServices.For(_options.CurrentValue.Endpoint);

    /// <summary>Where to get a key of the current service; <c>null</c> for a user's own endpoint.</summary>
    public Uri? KeyPage => Service?.KeyPage;

    /// <summary>Key dialog text: the current service by name; an own endpoint by host, with the protocol it needs.</summary>
    public string KeyPrompt()
    {
        if (Service is { } service)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyDialogPrompt, service.Title);
        }

        var endpoint = _options.CurrentValue.Endpoint;
        var host = Uri.TryCreate(endpoint, UriKind.Absolute, out var address) ? address.Host : endpoint;
        return string.Format(CultureInfo.CurrentCulture, Strings.ApiKeyDialogPromptCustom, host);
    }

    private string SecretName => AgentServices.SecretFor(_options.CurrentValue.Endpoint);

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

    public void Dispose() => _subscription?.Dispose();

    private void OnOptionsChanged(AgentOptions options, string? name)
    {
        var changed = AgentServices.SecretFor(options.Endpoint) != AgentServices.SecretFor(_endpoint);
        _endpoint = options.Endpoint;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
