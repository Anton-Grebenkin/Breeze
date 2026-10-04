using System.Globalization;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// Branch picker in the palette, like "Git: Checkout to…" in VS Code: "Create branch…" first, then local branches (most
/// recent first), then remote branches without a local counterpart; switching to one creates a local branch. A new
/// branch name is typed in the same palette and validated before git runs.
/// </summary>
public sealed class GitBranchPicker(GitRepository repository, GitReader reader, GitActions actions, IQuickPick quickPick)
{
    private const string CreateItemId = "+create";
    private const string LocalPrefix = "local:";
    private const string RemotePrefix = "remote:";

    public async Task PickAsync()
    {
        if (repository.Location is not { } location)
        {
            repository.ReportError(Strings.NoRepository);
            return;
        }

        IReadOnlyList<GitBranch> branches;
        try
        {
            branches = await reader.BranchesAsync(location.Root, CancellationToken.None);
        }
        catch (GitException exception)
        {
            repository.ReportError(exception.Message);
            return;
        }

        var byId = new Dictionary<string, GitBranch>(StringComparer.Ordinal);
        var items = new List<QuickPickItem> { new(CreateItemId, Strings.CreateBranchItem) };
        var locals = branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var branch in branches.Where(branch => !branch.IsRemote || !locals.Contains(branch.LocalName)))
        {
            var id = (branch.IsRemote ? RemotePrefix : LocalPrefix) + branch.Name;
            byId[id] = branch;
            items.Add(new QuickPickItem(id, branch.Name) { Detail = Detail(branch) });
        }

        quickPick.Show(new QuickPickProvider(Strings.CheckoutPlaceholder, items, item => AcceptAsync(item, byId)));
    }

    /// <summary>Prompts for a new branch name; the "Create branch '…'" item appears only for a valid name.</summary>
    public void ShowCreate() =>
        quickPick.Show(new QuickPickProvider(Strings.NewBranchPlaceholder, [], item => actions.CreateBranchAsync(item.Id))
        {
            CustomItem = text => GitArguments.IsValidBranchName(text) ? new QuickPickItem(text, Format(Strings.CreateBranchNamed, text)) : null,
            EmptyText = Strings.BranchNameHint,
        });

    private Task AcceptAsync(QuickPickItem item, Dictionary<string, GitBranch> byId)
    {
        if (item.Id == CreateItemId)
        {
            ShowCreate();
            return Task.CompletedTask;
        }

        return byId.TryGetValue(item.Id, out var branch) && !branch.IsCurrent ? actions.SwitchAsync(branch) : Task.CompletedTask;
    }

    private static string Detail(GitBranch branch)
    {
        var commit = branch.Subject.Length == 0 ? branch.Commit : $"{branch.Commit} • {branch.Subject}";
        return branch.IsCurrent ? Format(Strings.CurrentBranchDetail, commit)
            : branch.IsRemote ? Format(Strings.RemoteBranchDetail, commit)
            : commit;
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
