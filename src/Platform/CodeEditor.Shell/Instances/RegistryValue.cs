namespace CodeEditor.Shell.Instances;

/// <summary>A string value under <c>HKCU\Software\Classes</c>.</summary>
/// <param name="Key">The key path relative to <c>Software\Classes</c>.</param>
/// <param name="Name">The value name; <c>null</c> for the key's default value.</param>
public sealed record RegistryValue(string Key, string? Name, string Data);
