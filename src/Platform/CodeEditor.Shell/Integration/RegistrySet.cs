namespace CodeEditor.Shell.Integration;

/// <summary>
/// The registry entries of one Explorer feature. Removing it deletes <see cref="OwnedKeys"/> with their subkeys and,
/// in keys shared with Windows and other apps (<c>.cs\OpenWithProgids</c>, <c>RegisteredApplications</c>), only the
/// named values Breeze added.
/// </summary>
public sealed record RegistrySet(IReadOnlyList<string> OwnedKeys, IReadOnlyList<RegistryValue> Values)
{
    public IEnumerable<RegistryValue> SharedValues => Values.Where(value => !IsOwned(value.Key));

    public bool IsOwned(string key) =>
        OwnedKeys.Any(owned => key.Equals(owned, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(owned + @"\", StringComparison.OrdinalIgnoreCase));
}
