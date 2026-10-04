using System.Collections.Frozen;

namespace CodeEditor.Architecture.Tests.Infrastructure;

/// <summary>
/// Product assemblies and the allowed dependencies between them (CONTRIBUTING.md, "Architecture").
/// A module references only the platform and other modules' *.Contracts, never another module's implementation.
/// </summary>
internal static class Layers
{
    public const string Core = "CodeEditor.Core";
    public const string Shell = "CodeEditor.Shell";
    public const string UI = "CodeEditor.UI";
    public const string ShellWpf = "CodeEditor.Shell.Wpf";
    public const string App = "Breeze";

    public const string OutputModule = "CodeEditor.Modules.Output";
    public const string OutputModuleWpf = "CodeEditor.Modules.Output.Wpf";
    public const string ExplorerModule = "CodeEditor.Modules.Explorer";
    public const string ExplorerModuleWpf = "CodeEditor.Modules.Explorer.Wpf";
    public const string TextEditorModule = "CodeEditor.Modules.TextEditor";
    public const string TextEditorModuleWpf = "CodeEditor.Modules.TextEditor.Wpf";
    public const string SearchModule = "CodeEditor.Modules.Search";
    public const string SearchModuleWpf = "CodeEditor.Modules.Search.Wpf";
    public const string AgentContracts = "CodeEditor.Modules.Agent.Contracts";
    public const string AgentModule = "CodeEditor.Modules.Agent";
    public const string AgentModuleWpf = "CodeEditor.Modules.Agent.Wpf";
    public const string TerminalModule = "CodeEditor.Modules.Terminal";
    public const string GitModule = "CodeEditor.Modules.Git";
    public const string GitModuleWpf = "CodeEditor.Modules.Git.Wpf";
    public const string DockerModule = "CodeEditor.Modules.Docker";
    public const string DockerModuleWpf = "CodeEditor.Modules.Docker.Wpf";
    public const string BrowserModule = "CodeEditor.Modules.Browser";
    public const string BrowserModuleWpf = "CodeEditor.Modules.Browser.Wpf";
    public const string DiagramsModule = "CodeEditor.Modules.Diagrams";
    public const string DiagramsModuleWpf = "CodeEditor.Modules.Diagrams.Wpf";
    public const string DocumentsModule = "CodeEditor.Modules.Documents";
    public const string DocumentsModuleWpf = "CodeEditor.Modules.Documents.Wpf";
    public const string ViewersModule = "CodeEditor.Modules.Viewers";
    public const string ViewersModuleWpf = "CodeEditor.Modules.Viewers.Wpf";
    public const string ToolsModule = "CodeEditor.Modules.Tools";
    public const string UpdatesModule = "CodeEditor.Modules.Updates";

    private const string ProductPrefix = "CodeEditor.";

    /// <summary>WPF assemblies forbidden in logic layers.</summary>
    public static FrozenSet<string> WpfAssemblies { get; } = new[]
    {
        "PresentationFramework",
        "PresentationCore",
        "WindowsBase",
        "System.Xaml",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Logic assemblies: net10.0 without WPF.</summary>
    public static IReadOnlyList<string> LogicAssemblies { get; } = [Core, Shell, OutputModule, ExplorerModule, TextEditorModule, SearchModule, AgentContracts, AgentModule, TerminalModule, GitModule, DockerModule, BrowserModule, DiagramsModule, DocumentsModule, ViewersModule, ToolsModule, UpdatesModule];

    /// <summary>The CodeEditor assemblies each product assembly may reference.</summary>
    public static FrozenDictionary<string, FrozenSet<string>> AllowedReferences { get; } =
        new Dictionary<string, string[]>
        {
            [Core] = [],
            [Shell] = [Core],
            [UI] = [],
            [ShellWpf] = [Core, Shell, UI],
            [OutputModule] = [Core, Shell],
            [OutputModuleWpf] = [Core, Shell, UI, ShellWpf, OutputModule],
            [ExplorerModule] = [Core, Shell, AgentContracts],
            [ExplorerModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, ExplorerModule],
            [TextEditorModule] = [Core, Shell, AgentContracts],
            [TextEditorModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, TextEditorModule],
            [SearchModule] = [Core, Shell, AgentContracts],
            [SearchModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, SearchModule],
            [AgentContracts] = [Core],
            [AgentModule] = [Core, Shell, AgentContracts],
            [AgentModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, AgentModule],
            [TerminalModule] = [Core, Shell, AgentContracts],
            [GitModule] = [Core, Shell, AgentContracts],
            [GitModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, GitModule],
            [DockerModule] = [Core, Shell, AgentContracts],
            [DockerModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, DockerModule],
            [BrowserModule] = [Core, Shell, AgentContracts],
            [BrowserModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, BrowserModule],
            [DiagramsModule] = [Core, Shell, AgentContracts],
            [DiagramsModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, DiagramsModule],
            [DocumentsModule] = [Core, Shell, AgentContracts],
            [DocumentsModuleWpf] = [Core, Shell, UI, ShellWpf, AgentContracts, DocumentsModule],
            [ViewersModule] = [Core, Shell],
            [ViewersModuleWpf] = [Core, Shell, UI, ShellWpf, ViewersModule],
            [ToolsModule] = [Core, Shell, AgentContracts],
            [UpdatesModule] = [Core, Shell],
            [App] = [Core, Shell, UI, ShellWpf, OutputModule, OutputModuleWpf, ExplorerModule, ExplorerModuleWpf, TextEditorModule, TextEditorModuleWpf, SearchModule, SearchModuleWpf, AgentContracts, AgentModule, AgentModuleWpf, TerminalModule, GitModule, GitModuleWpf, DockerModule, DockerModuleWpf, BrowserModule, BrowserModuleWpf, DiagramsModule, DiagramsModuleWpf, DocumentsModule, DocumentsModuleWpf, ViewersModule, ViewersModuleWpf, ToolsModule, UpdatesModule],
        }.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

    public static bool IsProductAssembly(string name) => name.StartsWith(ProductPrefix, StringComparison.Ordinal);
}
