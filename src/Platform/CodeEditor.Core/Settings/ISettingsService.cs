using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace CodeEditor.Core.Settings;

/// <summary>
/// Settings, as in VS Code: the user <c>settings.json</c> with the open folder's <c>.breeze/settings.json</c> on
/// top. Files reload when changed on disk; typed sections come through <c>IOptionsMonitor&lt;T&gt;</c>
/// (<see cref="SettingsServiceCollectionExtensions.AddSettingsSection{T}"/>). Notifications arrive on the UI thread.
/// </summary>
public interface ISettingsService
{
    IConfiguration Configuration { get; }

    string UserSettingsPath { get; }

    /// <summary>The open folder's settings file; <c>null</c> without a folder.</summary>
    string? WorkspaceSettingsPath { get; }

    /// <summary>Parse error in the user or folder file; the last valid values stay in effect.</summary>
    string? Error { get; }

    event EventHandler? Changed;

    /// <summary>
    /// Writes a value to user settings, keeping comments and order; <c>null</c> removes the key (back to default).
    /// Returns <c>false</c> when the file is corrupt or unavailable (reason in <paramref name="error"/>); as in
    /// VS Code, the file must be fixed first.
    /// </summary>
    bool TrySetUserValue(string key, object? value, [NotNullWhen(false)] out string? error);

    /// <summary>Creates the user file with a hint if missing and returns its path.</summary>
    string EnsureUserSettingsFile();

    /// <summary>Creates the folder's <c>.breeze/settings.json</c> if missing; <c>null</c> without a folder.</summary>
    string? EnsureWorkspaceSettingsFile();
}
