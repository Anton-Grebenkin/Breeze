using System.Globalization;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services.Matching;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Search.ViewModels;

/// <summary>A result row: the line preview with the match highlighted.</summary>
public sealed class SearchMatchViewModel(SearchFileViewModel file, SearchMatch match)
{
    public SearchFileViewModel File { get; } = file;

    public int Line => match.Line;

    public string Before { get; } = match.Preview[..match.PreviewStart].TrimStart();

    public string Text { get; } = match.Preview.Substring(match.PreviewStart, match.PreviewLength);

    public string After { get; } = match.Preview[(match.PreviewStart + match.PreviewLength)..];

    public EditorLocation Location { get; } = new(match.Line, match.Column, match.Length);

    public string AutomationId => $"Search.Match.{File.RelativePath}:{match.Line}:{match.Column}";

    public string ToolTip => string.Format(CultureInfo.CurrentCulture, Strings.LineColumn, match.Line, match.Column);
}
