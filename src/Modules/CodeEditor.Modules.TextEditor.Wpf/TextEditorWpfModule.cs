using System.Windows;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Modules.TextEditor.Wpf.Buffers;
using CodeEditor.Modules.TextEditor.Wpf.Highlighting;
using CodeEditor.Modules.TextEditor.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using CodeEditor.UI.Markdown;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.TextEditor.Wpf;

/// <summary>
/// AvalonEdit editor: the document buffer factory (ADR 0003), the view, Edit menu commands and highlighting of
/// Markdown code blocks (agent chat).
/// </summary>
public sealed class TextEditorWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("texteditor.wpf", Strings.ViewModuleName)
    {
        Dependencies = [TextEditorModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITextBufferFactory, AvalonTextBufferFactory>();
        services.AddSingleton<ThemedHighlighting>();
        services.AddSingleton<ICodeColorizer, CodeBlockColorizer>();
        services.AddSingleton<ClipboardCommands>();
    }

    public void Contribute(IServiceProvider services)
    {
        // The search panel style goes into app resources: the panel lives in the window's adorner layer.
        services.GetRequiredService<Application>().Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/CodeEditor.Modules.TextEditor.Wpf;component/Themes/SearchPanel.xaml"),
        });

        var highlighting = services.GetRequiredService<ThemedHighlighting>();
        var fontZoom = services.GetRequiredService<EditorFontZoom>();
        services.GetRequiredService<IViewRegistry>().Register<TextEditorViewModel>(_ => new TextEditorView(highlighting, fontZoom));
        services.GetRequiredService<ClipboardCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
    }
}
