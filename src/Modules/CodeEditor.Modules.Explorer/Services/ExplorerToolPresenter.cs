using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Explorer.Resources;

namespace CodeEditor.Modules.Explorer.Services;

/// <summary>
/// Explorer tool rows in the agent feed (ADR 0010), e.g. "Read Order.cs · lines 1–80", "Listed src · 12 items",
/// "Found files "*.cs" · 5 files", "Deleted a.txt", "Moved a.cs → b.cs".
/// </summary>
public sealed class ExplorerToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            ExplorerAgentTools.ReadFileName => ReadFile(call),
            ExplorerAgentTools.ListDirName => ListDir(call),
            ExplorerAgentTools.FindFilesName => FindFiles(call),
            ExplorerEditingTools.DeleteFileName => Delete(call),
            ExplorerEditingTools.MoveFileName => Move(call),
            _ => null,
        };
    }

    private static AgentToolView ReadFile(AgentToolCall call)
    {
        var path = call.Text("path") ?? "?";
        var title = Format(call.IsDone ? Strings.ReadFileDone : Strings.ReadFileRunning, ToolText.FileName(path));
        return new AgentToolView(AgentToolIcon.Read, title)
        {
            FilePath = path,
            IsExploration = true,
            Detail = call.Result is { } result ? LineRange(result) : null,
        };
    }

    private static AgentToolView ListDir(AgentToolCall call)
    {
        var title = call.Text("path") is { } folder && folder != "."
            ? Format(call.IsDone ? Strings.ListDirDone : Strings.ListDirRunning, folder)
            : call.IsDone ? Strings.ListRootDone : Strings.ListRootRunning;
        return new AgentToolView(AgentToolIcon.Folder, title)
        {
            IsExploration = true,
            Detail = call.Result is { } result ? Plural.Format(Lines(result), Strings.ItemForms) : null,
        };
    }

    private static AgentToolView FindFiles(AgentToolCall call)
    {
        var pattern = call.Text("pattern") ?? "?";
        return new AgentToolView(AgentToolIcon.Search, Format(call.IsDone ? Strings.FindFilesDone : Strings.FindFilesRunning, pattern))
        {
            IsExploration = true,
            Detail = call.Result is { } result ? Plural.Format(Lines(result), Strings.FileForms) : null,
        };
    }

    private static AgentToolView Delete(AgentToolCall call)
    {
        var name = ToolText.FileName(call.Text("path") ?? "?");
        return new AgentToolView(AgentToolIcon.Delete, Format(call.IsDone ? Strings.DeleteDone : Strings.DeleteRunning, name));
    }

    private static AgentToolView Move(AgentToolCall call)
    {
        var from = ToolText.FileName(call.Text("from") ?? "?");
        var to = call.Text("to") ?? "?";
        return new AgentToolView(AgentToolIcon.Move, Format(call.IsDone ? Strings.MoveDone : Strings.MoveRunning, from, to)) { FilePath = to };
    }

    /// <summary>read_file rows are "number\ttext"; the first and last give the range.</summary>
    private static string? LineRange(string result)
    {
        if (result.StartsWith(Strings.ToolEmptyFile, StringComparison.Ordinal))
        {
            return Strings.LinesEmpty;
        }

        if (result.StartsWith(FormatPrefix(Strings.ToolLinesUnchanged), StringComparison.Ordinal))
        {
            return Strings.LinesUnchanged;
        }

        var numbers = result.Split('\n').Select(LineNumber).Where(number => number > 0).ToList();
        return numbers.Count == 0 ? null : Format(Strings.LinesRange, numbers[0], numbers[^1]);
    }

    private static int LineNumber(string line)
    {
        var tab = line.IndexOf('\t', StringComparison.Ordinal);
        return tab > 0 && int.TryParse(line.AsSpan(0, tab), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    // Output rows, excluding empty lines and "…(more …)" notes.
    private static int Lines(string result) =>
        result.Split('\n').Count(line => line.Length > 0 && !line.StartsWith('…') && !line.StartsWith('('));

    // The fixed start of a format string, up to the first placeholder, identifies a tool message.
    private static string FormatPrefix(string format) => format[..format.IndexOf('{', StringComparison.Ordinal)];

    private static string Format(string format, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, format, arguments);
}
