using CodeEditor.Modules.Updates.Services;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Updates.Tests;

/// <summary>A build run from a build folder (like the tests) is not installed: nothing to check, nothing to throw.</summary>
public sealed class VelopackUpdaterTests
{
    [Fact]
    public void LocalBuild_WithoutRepository_IsNotInstalled() =>
        Assert.False(new VelopackUpdater(new ProductInfo("Breeze", "0.1.0-dev", null, null)).IsInstalled);

    [Fact]
    public void ReleaseBuild_RunFromBuildFolder_IsNotInstalled() =>
        Assert.False(new VelopackUpdater(new ProductInfo("Breeze", "0.1.0-alpha.1", "abc", new Uri("https://github.com/owner/breeze"))).IsInstalled);

    [Fact]
    public async Task NotInstalled_CheckIsAProgrammingError()
    {
        var updater = new VelopackUpdater(new ProductInfo("Breeze", "0.1.0-dev", null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => updater.FindUpdateAsync(TestContext.Current.CancellationToken));
    }
}
