using System.Runtime.Versioning;
using CodeEditor.Shell.Instances;
using Microsoft.Win32;

namespace CodeEditor.Shell.Tests.Instances;

/// <summary>Writes into a scratch key under <c>HKCU\Software</c> standing in for <c>HKEY_CURRENT_USER</c>.</summary>
[SupportedOSPlatform("windows")]
public sealed class ExplorerRegistryTests : IDisposable
{
    private const string ScratchParent = @"Software\BreezeTests";
    private const string Launcher = @"C:\Breeze\Breeze.exe";

    private readonly string _scratch = ScratchParent + @"\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    private readonly IReadOnlyList<RegistryValue> _values = Values("Open in Breeze");

    public ExplorerRegistryTests() => _root = Registry.CurrentUser.CreateSubKey(_scratch);

    [Fact]
    public void Write_Twice_ChangesNothingTheSecondTime()
    {
        Assert.True(ExplorerRegistry.Write(_root, _values));

        Assert.False(ExplorerRegistry.Write(_root, _values));
        Assert.Equal($"\"{Launcher}\" \"%V\"", Read(@"Software\Classes\Directory\shell\Breeze\command", null));
    }

    [Fact]
    public void Write_RestoresAChangedOrMissingValue()
    {
        ExplorerRegistry.Write(_root, _values);
        _root.DeleteSubKeyTree(@"Software\Classes\Breeze.cs");

        Assert.True(ExplorerRegistry.Write(_root, Values("Открыть в Breeze")));
        Assert.Equal($"\"{Launcher}\" \"%1\"", Read(@"Software\Classes\Breeze.cs\shell\open\command", null));
        Assert.Equal("Открыть в Breeze", Read(@"Software\Classes\*\shell\Breeze", null));
    }

    [Fact]
    public void Remove_LeavesOtherAppsAndSharedKeys()
    {
        using (var other = _root.CreateSubKey(@"Software\Classes\.cs\OpenWithProgids"))
        {
            other.SetValue("VSCode.cs", string.Empty);
        }

        ExplorerRegistry.Write(_root, _values);
        ExplorerRegistry.Remove(_root, _values);

        using var openWith = _root.OpenSubKey(@"Software\Classes\.cs\OpenWithProgids");
        Assert.Equal(["VSCode.cs"], openWith!.GetValueNames());
        Assert.All(ExplorerIntegration.OwnedKeys, key => Assert.Null(_root.OpenSubKey(key)));
        using var registered = _root.OpenSubKey(@"Software\RegisteredApplications");
        Assert.Empty(registered!.GetValueNames());
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_scratch, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(ScratchParent, writable: true);
        if (parent is { SubKeyCount: 0 })
        {
            Registry.CurrentUser.DeleteSubKey(ScratchParent, throwOnMissingSubKey: false);
        }
    }

    private static IReadOnlyList<RegistryValue> Values(string openIn) =>
        ExplorerIntegration.Values(Launcher, new ExplorerTexts(openIn, "{0} File", "Code editor"));

    private string? Read(string key, string? name)
    {
        using var opened = _root.OpenSubKey(key);
        return opened?.GetValue(name) as string;
    }
}
