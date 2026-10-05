using CodeEditor.Shell.Integration;

namespace CodeEditor.Shell.Tests.Integration;

public sealed class ExplorerIntegrationTests
{
    private const string Launcher = @"C:\Users\me\AppData\Local\BreezeCodeEditor\Breeze.exe";
    private const string OpenFile = $"\"{Launcher}\" \"%1\"";
    private const string Icon = $"\"{Launcher}\",0";

    private readonly RegistrySet _contextMenu = ExplorerIntegration.ContextMenu(Launcher, "Открыть в Breeze");
    private readonly RegistrySet _fileTypes = ExplorerIntegration.FileTypes(Launcher, "Файл {0}", "Редактор кода");

    [Theory]
    [InlineData(@"Software\Classes\*\shell\Breeze", "%1")]
    [InlineData(@"Software\Classes\Directory\shell\Breeze", "%V")]
    [InlineData(@"Software\Classes\Directory\Background\shell\Breeze", "%V")]
    public void ContextMenu_OnFilesFoldersAndFolderBackground(string key, string argument)
    {
        Assert.Contains(new RegistryValue(key, null, "Открыть в Breeze"), _contextMenu.Values);
        Assert.Contains(new RegistryValue(key, "Icon", Icon), _contextMenu.Values);
        Assert.Contains(new RegistryValue(key + @"\command", null, $"\"{Launcher}\" \"{argument}\""), _contextMenu.Values);
    }

    // The context menu changes no file type: turning it on or off never touches icons or "Open with".
    [Fact]
    public void ContextMenu_OwnsAllItsKeys_AndNothingOfFileTypes()
    {
        Assert.Empty(_contextMenu.SharedValues);
        Assert.DoesNotContain(_contextMenu.Values, value => value.Key.Contains("OpenWith", StringComparison.Ordinal));
        Assert.All(_fileTypes.Values, value => Assert.False(_contextMenu.IsOwned(value.Key), value.Key));
    }

    [Fact]
    public void FileTypes_ListBreezeInOpenWith()
    {
        Assert.Contains(new RegistryValue(@"Software\Classes\*\OpenWithList\Breeze.exe", null, string.Empty), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Applications\Breeze.exe\shell\open\command", null, OpenFile), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Applications\Breeze.exe", "FriendlyAppName", "Breeze"), _fileTypes.Values);
    }

    // The "Open with" submenu and "Default apps" take apps from the extension's OpenWithProgids and the app's capabilities.
    [Fact]
    public void FileType_HasItsOwnProgIdOfferedForTheExtension()
    {
        Assert.Contains(new RegistryValue(@"Software\Classes\Breeze.cs", null, "Файл CS"), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Breeze.cs\DefaultIcon", null, Icon), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Breeze.cs\shell\open\command", null, OpenFile), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\Classes\.cs\OpenWithProgids", "Breeze.cs", string.Empty), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\Classes\Applications\Breeze.exe\SupportedTypes", ".cs", string.Empty), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\BreezeCodeEditor\Capabilities\FileAssociations", ".cs", "Breeze.cs"), _fileTypes.Values);
    }

    [Fact]
    public void FileTypes_ListBreezeInDefaultApps()
    {
        Assert.Contains(new RegistryValue(@"Software\RegisteredApplications", "Breeze", @"Software\BreezeCodeEditor\Capabilities"), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\BreezeCodeEditor\Capabilities", "ApplicationName", "Breeze"), _fileTypes.Values);
        Assert.Contains(new RegistryValue(@"Software\BreezeCodeEditor\Capabilities", "ApplicationDescription", "Редактор кода"), _fileTypes.Values);
    }

    // Removal deletes owned keys whole and only Breeze's named values elsewhere: .cs or RegisteredApplications stay.
    [Fact]
    public void FileTypes_OutsideOwnedKeys_HaveOnlyNamedValuesInSharedKeys()
    {
        var shared = _fileTypes.SharedValues.ToList();

        Assert.Equal(OpenWithFileTypes.Extensions.Length + 1, shared.Count);
        Assert.All(shared, value => Assert.NotNull(value.Name));
        Assert.All(shared, value => Assert.True(
            value.Key == @"Software\RegisteredApplications" || value.Key.EndsWith(@"\OpenWithProgids", StringComparison.Ordinal),
            value.Key));
    }

    [Theory]
    [InlineData(@"Software\Classes\.cs")]
    [InlineData(@"Software\Classes\*")]
    [InlineData(@"Software\Classes\Directory")]
    [InlineData(@"Software\Classes\Applications")]
    [InlineData(@"Software\Classes")]
    public void SharedKeys_AreNotOwned(string key)
    {
        Assert.False(_fileTypes.IsOwned(key));
        Assert.False(_contextMenu.IsOwned(key));
    }

    [Fact]
    public void OwnedKeys_IncludeSubkeys_IgnoringCase() =>
        Assert.True(_fileTypes.IsOwned(@"software\classes\breeze.CS\shell\open"));
}
