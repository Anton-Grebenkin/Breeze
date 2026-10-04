using System.Text.Json;
using CodeEditor.Core;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Storage;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Explorer;
using CodeEditor.Modules.Docker;
using CodeEditor.Modules.Documents;
using CodeEditor.Modules.Git;
using CodeEditor.Modules.Output;
using CodeEditor.Modules.Search;
using CodeEditor.Modules.Terminal;
using CodeEditor.Modules.TextEditor;
using CodeEditor.Modules.Tools;
using CodeEditor.Shell;
using CodeEditor.Shell.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Headless agent container: the core and logic modules as in the app, with its own UI thread, an editor-less text
/// buffer and stub dialogs instead of WPF. Settings live in <c>settings.json</c> in a temp user folder and the log in
/// its <c>logs</c>, as in the app; the log shows every model request (tokens, cache, misses).
/// </summary>
internal static class EvalHost
{
    /// <param name="endpoint">Service endpoint, which names the key's secret (<see cref="AgentServices"/>).</param>
    public static ServiceProvider Build(string userData, EvalDispatcher dispatcher, string apiKey, bool dryRun, string endpoint = AgentOptions.DefaultEndpoint)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new LogFileWriter(Path.Combine(userData, LogFileWriter.FolderName), "CodeEditor.Agent.Eval", TimeProvider.System));
        services.AddLogging(logging => logging.Services.AddSingleton<ILoggerProvider>(provider =>
            new FileLoggerProvider(provider.GetRequiredService<LogFileWriter>(), new LogLevelSwitch(LogLevel.Information), TimeProvider.System)));
        services.AddCodeEditorCore();
        new ModuleLoader(ModuleCatalog.Create(Modules()), NullLogger<ModuleLoader>.Instance).ConfigureServices(services);

        // Overrides go after the modules: the last registration of a single service wins.
        services.AddSingleton(new UserDataPaths(userData));
        services.AddSingleton<ISecretStore>(new MemorySecretStore(AgentServices.SecretFor(endpoint), apiKey));
        services.AddSingleton<IUiDispatcher>(dispatcher);
        services.AddSingleton<ITextBufferFactory, TestTextBufferFactory>();
        services.AddSingleton<IDialogService, FakeDialogs>();
        services.AddSingleton<ISystemShell, FakeSystemShell>();
        if (dryRun)
        {
            services.AddSingleton<IChatClientFactory, DryRunChatClientFactory>();
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Agent settings for a run: model, mode, endpoint, Deep mode advisor (ADR 0012); edits are accepted immediately,
    /// as for an autonomous agent.
    /// </summary>
    public static void WriteSettings(string userData, string model, string mode, EvalOptions options)
    {
        Directory.CreateDirectory(userData);
        var settings = new Dictionary<string, object>
        {
            ["agent.endpoint"] = options.Endpoint,
            ["agent.model"] = model,
            ["agent.mode"] = mode,
            ["agent.approvals"] = "auto",
            // Post-turn memory would write notes into the task project and spend tokens; runs must be repeatable.
            ["agent.autoMemory"] = false,
        };
        if (options.AdvisorModel is { } advisor)
        {
            settings["agent.advisorModel"] = advisor;
        }

        if (options.Reasoning is { } reasoning)
        {
            settings["agent.reasoningEffort"] = reasoning;
        }

        if (options.Api is { } api)
        {
            settings["agent.api"] = api;
        }

        if (options.Trace)
        {
            settings["agent.trafficLog"] = true;
        }

        File.WriteAllText(Path.Combine(userData, "settings.json"), JsonSerializer.Serialize(settings));
    }

    private static IModule[] Modules() =>
    [
        new ShellModule(),
        new OutputModule(),
        new ExplorerModule(),
        new SearchModule(),
        new AgentModule(),
        new TextEditorModule(),
        new TerminalModule(),
        new GitModule(),
        new DockerModule(),
        new DocumentsModule(),
        new ToolsModule(),
    ];
}
