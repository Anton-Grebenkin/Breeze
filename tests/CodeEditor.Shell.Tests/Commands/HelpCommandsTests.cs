using CodeEditor.Core.Commands;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Commands;

public sealed class HelpCommandsTests : IDisposable
{
    private static readonly Uri Repository = new("https://github.com/owner/breeze");

    private readonly ShellFixture _shell = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeSystemShell _system = new();
    private HelpCommands? _commands;

    public void Dispose()
    {
        _commands?.Dispose();
        _shell.Dispose();
    }

    [Fact]
    public async Task About_ShowsVersionAndShortCommit()
    {
        Register(new ProductInfo("Breeze", "0.1.0-alpha.1", "0123456789abcdef", Repository));

        await Execute(HelpCommands.AboutId);

        var (message, detail) = Assert.Single(_dialogs.Information);
        Assert.Equal("Breeze", message);
        Assert.StartsWith("Версия: 0.1.0-alpha.1\nКоммит: 0123456\n", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task About_LocalBuild_SaysSoInsteadOfCommit()
    {
        Register(new ProductInfo("Breeze", "0.1.0-dev", null, null));

        await Execute(HelpCommands.AboutId);

        Assert.Contains("Коммит: локальная сборка", Assert.Single(_dialogs.Information).Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HelpCommands.OpenRepositoryId, "https://github.com/owner/breeze")]
    [InlineData(HelpCommands.ReleaseNotesId, "https://github.com/owner/breeze/releases/tag/v0.1.0-alpha.1")]
    [InlineData(HelpCommands.ReportIssueId, "https://github.com/owner/breeze/issues/new/choose")]
    public async Task Links_OpenRepositoryPagesInBrowser(string commandId, string expected)
    {
        Register(new ProductInfo("Breeze", "0.1.0-alpha.1", "abc", new Uri("https://github.com/owner/breeze/")));

        await Execute(commandId);

        Assert.Equal(new Uri(expected), _system.Opened);
    }

    [Fact]
    public void LocalBuild_HelpMenuHasOnlyAbout()
    {
        Register(new ProductInfo("Breeze", "0.1.0-dev", null, null));

        Assert.Equal([HelpCommands.AboutId], _shell.Menus.GetItems(MenuIds.Help).Select(item => item.CommandId));
        Assert.False(_shell.Commands.TryGet(HelpCommands.ReportIssueId, out _));
    }

    private void Register(ProductInfo product)
    {
        _commands = new HelpCommands(product, _dialogs, _system);
        _commands.Register(_shell.Commands, _shell.Menus);
    }

    private async Task Execute(string commandId) =>
        Assert.Equal(CommandExecutionStatus.Succeeded, await _shell.CommandService.ExecuteAsync(commandId, cancellationToken: TestContext.Current.CancellationToken));
}
