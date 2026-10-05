namespace CodeEditor.Shell.Integration;

/// <summary>A string value under <c>HKEY_CURRENT_USER</c>.</summary>
/// <param name="Key">The key path relative to <c>HKEY_CURRENT_USER</c>.</param>
/// <param name="Name">The value name; <c>null</c> for the key's default value.</param>
public sealed record RegistryValue(string Key, string? Name, string Data);
