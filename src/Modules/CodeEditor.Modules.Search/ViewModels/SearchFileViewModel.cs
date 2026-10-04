using CodeEditor.Core.Text;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services.Matching;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Search.ViewModels;

/// <summary>A collapsible file in the results: name, folder and its matches.</summary>
public sealed partial class SearchFileViewModel : ObservableObject
{
    public SearchFileViewModel(FileSearchResult result)
    {
        FullPath = result.FullPath;
        RelativePath = result.RelativePath;
        Name = Path.GetFileName(result.RelativePath);
        var slash = result.RelativePath.LastIndexOf('/');
        Directory = slash > 0 ? result.RelativePath[..slash] : string.Empty;
        Matches = [.. result.Matches.Select(match => new SearchMatchViewModel(this, match))];
    }

    public string FullPath { get; }

    public string RelativePath { get; }

    public string Name { get; }

    public string Directory { get; }

    public IReadOnlyList<SearchMatchViewModel> Matches { get; }

    public int Count => Matches.Count;

    public string CountToolTip => Plural.Format(Count, Strings.MatchForms);

    public string AutomationId => $"Search.File.{RelativePath}";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;
}
