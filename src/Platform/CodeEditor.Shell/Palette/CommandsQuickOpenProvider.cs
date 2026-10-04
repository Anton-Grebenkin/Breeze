using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.Palette;

/// <summary>The ">" mode: all available commands with keybindings, recent ones first.</summary>
public sealed class CommandsQuickOpenProvider(
    ICommandRegistry commands,
    IKeybindingRegistry keybindings,
    ICommandService commandService,
    IContextKeyService context,
    RecentCommands recent) : IQuickOpenProvider
{
    public const string CommandsPrefix = ">";

    private static readonly HashSet<string> PaletteCommands =
        [ShellCommandIds.ShowCommands, ShellCommandIds.QuickOpen, ShellCommandIds.GoToLine, ShellCommandIds.EditorGoToLine];

    private PaletteCandidate[] _candidates = [];

    public string Prefix => CommandsPrefix;

    public string Placeholder => Strings.CommandsPlaceholder;

    public string EmptyText => Strings.NoMatchingCommands;

    // `when` clauses are evaluated on entering the mode: the palette shows what is available at that moment.
    public void Prepare() =>
        _candidates =
        [
            .. commands.Commands
                .Where(command => !PaletteCommands.Contains(command.Id) && context.Evaluate(command.When))
                .Select(command => new PaletteCandidate(command, keybindings.FindForCommand(command.Id)?.Sequence.ToString())),
        ];

    public IReadOnlyList<PaletteItem> Filter(string text) => PaletteFilter.Apply(_candidates, text, recent.Items);

    public async Task AcceptAsync(PaletteItem item, string text)
    {
        recent.Add(item.Id);
        await commandService.ExecuteAsync(item.Id);
    }
}
