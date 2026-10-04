using CodeEditor.Core;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Diagrams.Commands;
using CodeEditor.Modules.Diagrams.Services.Agent;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;
using CodeEditor.Shell;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.Theming;
using CodeEditor.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// Module composition in a container, as in the app: with a renderer there are commands, keys, menus, the agent tool and
/// its approvals; without one (a host without a window) there are no commands and no tool.
/// </summary>
public sealed class DiagramsModuleTests
{
    [Fact]
    public void WithRenderer_CommandsAndToolAreContributed()
    {
        using var provider = Build(new FakeDiagramRenderer());

        new DiagramsModule().Contribute(provider);

        Assert.True(provider.GetRequiredService<ICommandRegistry>().TryGet(DiagramCommands.OpenPreviewToSideId, out _));
        Assert.NotNull(provider.GetRequiredService<IKeybindingRegistry>().FindForCommand(DiagramCommands.OpenPreviewToSideId));
        Assert.Contains(provider.GetServices<IAgentToolProvider>().SelectMany(tools => tools.CreateTools()), tool => tool.Name == DiagramAgentTools.ToolName);
        Assert.Contains(provider.GetServices<IAgentApprovalPolicy>(), policy => policy.CanDecide(DiagramAgentTools.ToolName));
        Assert.Contains(provider.GetServices<IAgentChangePreviewer>(), previewer => previewer.CanPreview(DiagramAgentTools.ToolName));
    }

    [Fact]
    public void WithoutRenderer_NothingIsContributed()
    {
        using var provider = Build(renderer: null);

        new DiagramsModule().Contribute(provider);

        Assert.False(provider.GetRequiredService<ICommandRegistry>().TryGet(DiagramCommands.OpenPreviewToSideId, out _));
        Assert.DoesNotContain(provider.GetServices<IAgentToolProvider>().SelectMany(tools => tools.CreateTools()), tool => tool.Name == DiagramAgentTools.ToolName);
    }

    private static ServiceProvider Build(IDiagramRenderer? renderer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddCodeEditorCore();
        new ShellModule().ConfigureServices(services);
        new DiagramsModule().ConfigureServices(services);
        services.AddSingleton<IFileSystem>(new FakeFileSystem());
        services.AddSingleton<ITextBufferFactory, TestTextBufferFactory>();
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        services.AddSingleton<IDialogService, FakeDialogs>();
        services.AddSingleton<IThemeService, FakeThemeService>();
        services.AddSingleton<IAgentImages, FakeAgentImages>();
        if (renderer is not null)
        {
            services.AddSingleton(renderer);
        }

        return services.BuildServiceProvider();
    }
}
