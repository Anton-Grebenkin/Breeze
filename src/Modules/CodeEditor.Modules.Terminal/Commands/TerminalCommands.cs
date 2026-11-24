using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Output;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services.Build;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Terminal.Commands;

/// <summary>
/// Build (<c>Ctrl+Shift+B</c>, as in Visual Studio) and Run Tests for the user, using the agent's services.
/// The summary goes to the status bar, details to the Build and Tests output channels; a failed build brings its
/// channel forward, as Visual Studio shows the error list.
/// </summary>
public sealed class TerminalCommands(DotNetBuild build, DotNetTests tests, StatusBarViewModel statusBar, IOutputService output) : IDisposable
{
    public const string BuildId = "terminal.build";
    public const string RunTestsId = "terminal.runTests";


    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(keybindings);
        var workspaceOpen = ContextExpression.Parse(IWorkspace.OpenContextKey);
        _registrations.Add(commands.Register(new CommandDefinition(BuildId, Strings.BuildCommand, (_, token) => RunAsync(Strings.Building, () => BuildAsync(token)), Strings.Build, workspaceOpen)));
        _registrations.Add(commands.Register(new CommandDefinition(RunTestsId, Strings.RunTestsCommand, (_, token) => RunAsync(Strings.RunningTests, async () =>
            TestReportText.Summary(await tests.RunAsync(null, null, token))), Strings.Build, workspaceOpen)));
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+Shift+B"), BuildId)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private async Task<string> BuildAsync(CancellationToken cancellationToken)
    {
        var report = await build.BuildAsync(null, cancellationToken);
        if (!report.Succeeded)
        {
            output.Reveal(output.GetOrCreate(DotNetBuild.ChannelName));
        }

        return BuildReportText.Summary(report);
    }

    private async ValueTask RunAsync(string progress, Func<Task<string>> run)
    {
        statusBar.Message = progress;
        try
        {
            statusBar.Message = await run();
        }
        catch (AgentToolException exception)
        {
            statusBar.Message = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            statusBar.Message = exception.Message;
        }
    }
}
