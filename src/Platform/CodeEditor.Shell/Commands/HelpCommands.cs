using System.Globalization;
using System.Runtime.InteropServices;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// The Help menu, as in VS Code: the project page, this version's release notes and issue reports on GitHub (in the
/// default browser, where the user is signed in), and "About" with the version and commit. A local build doesn't
/// know its repository, so it has only "About".
/// </summary>
public sealed class HelpCommands(ProductInfo product, IDialogService dialogs, ISystemShell shell) : IDisposable
{
    public const string AboutId = "help.about";
    public const string OpenRepositoryId = "help.openRepository";
    public const string ReleaseNotesId = "help.releaseNotes";
    public const string ReportIssueId = "help.reportIssue";

    private const string LinksGroup = "2_links";
    private const string AboutGroup = "9_about";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(menus);
        Add(commands, menus, AboutId, Strings.About, Strings.AboutMenu, AboutGroup, order: 0, ShowAbout);
        if (product.Repository is null)
        {
            return;
        }

        AddLink(commands, menus, OpenRepositoryId, Strings.OpenRepository, Strings.OpenRepositoryMenu, order: 1, string.Empty);
        AddLink(commands, menus, ReleaseNotesId, Strings.ReleaseNotes, Strings.ReleaseNotesMenu, order: 2, $"releases/tag/v{product.Version}");
        AddLink(commands, menus, ReportIssueId, Strings.ReportIssue, Strings.ReportIssueMenu, order: 3, "issues/new/choose");
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // A page of the repository: new Uri(base, relative) would drop the repository name.
    private Uri RepositoryPage(string path) =>
        new($"{product.Repository!.AbsoluteUri.TrimEnd('/')}/{path}".TrimEnd('/'));

    private void AddLink(ICommandRegistry commands, IMenuRegistry menus, string id, string title, string menuTitle, int order, string path) =>
        Add(commands, menus, id, title, menuTitle, LinksGroup, order, (_, _) =>
        {
            shell.OpenInBrowser(RepositoryPage(path));
            return ValueTask.CompletedTask;
        });

    private void Add(ICommandRegistry commands, IMenuRegistry menus, string id, string title, string menuTitle, string group, int order, CommandHandler handler)
    {
        _registrations.Add(commands.Register(new CommandDefinition(id, title, handler, Strings.CategoryHelp)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Help, id, group, order, title: menuTitle)));
    }

    // A system message box: its text is copied with Ctrl+C, as the details of a bug report need.
    private ValueTask ShowAbout(object? argument, CancellationToken cancellationToken)
    {
        string[] lines =
        [
            Format(Strings.AboutVersion, product.Version),
            Format(Strings.AboutCommit, product.ShortCommit ?? Strings.AboutLocalBuild),
            Format(Strings.AboutRuntime, Environment.Version),
            Format(Strings.AboutOs, $"{RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}"),
        ];
        dialogs.Inform(product.Name, string.Join('\n', lines));
        return ValueTask.CompletedTask;
    }

    private static string Format(string template, object argument) =>
        string.Format(CultureInfo.CurrentCulture, template, argument);
}
