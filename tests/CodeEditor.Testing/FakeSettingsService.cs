using System.Diagnostics.CodeAnalysis;
using CodeEditor.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace CodeEditor.Testing;

/// <summary>
/// In-memory settings: written values go to <see cref="Written"/>; <see cref="WriteError"/> simulates a corrupt file.
/// </summary>
public sealed class FakeSettingsService : ISettingsService
{
    public Dictionary<string, object?> Written { get; } = [];

    public string? WriteError { get; set; }

    public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();

    public string UserSettingsPath { get; set; } = @"C:\user\settings.json";

    public string? WorkspaceSettingsPath { get; set; }

    public string? Error { get; set; }

    public event EventHandler? Changed;

    public bool TrySetUserValue(string key, object? value, [NotNullWhen(false)] out string? error)
    {
        error = WriteError;
        if (error is not null)
        {
            return false;
        }

        Written[key] = value;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public string EnsureUserSettingsFile() => UserSettingsPath;

    public string? EnsureWorkspaceSettingsFile() => WorkspaceSettingsPath;
}
