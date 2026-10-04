using System.Globalization;
using CodeEditor.Modules.Tools.Resources;
using CodeEditor.Modules.Tools.Services;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Modules.Tools.Commands;

/// <summary>Asks tool parameters one by one in the palette, like input boxes in VS Code; Esc cancels the run.</summary>
public sealed class ToolParameterPrompt(IQuickPick quickPick)
{
    /// <param name="run">Called with all values after the last parameter.</param>
    public Task AskAsync(ToolDefinition tool, Func<IReadOnlyDictionary<string, string>, Task> run)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(run);
        return NextAsync(tool, 0, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), run);
    }

    private Task NextAsync(ToolDefinition tool, int index, Dictionary<string, string> values, Func<IReadOnlyDictionary<string, string>, Task> run)
    {
        if (index == tool.Parameters.Count)
        {
            return run(values);
        }

        var parameter = tool.Parameters[index];
        var placeholder = string.Format(CultureInfo.CurrentCulture, Strings.ParameterPlaceholder, tool.Name, parameter.Name,
            parameter.Description.Length > 0 ? parameter.Description : parameter.Name);
        quickPick.Show(new QuickPickProvider(placeholder, [], item =>
        {
            values[parameter.Name] = item.Id;
            return NextAsync(tool, index + 1, values, run);
        })
        {
            CustomItem = text => new QuickPickItem(text, text.Length == 0 ? Strings.ParameterEmpty : string.Format(CultureInfo.CurrentCulture, Strings.ParameterValue, text)),
        });
        return Task.CompletedTask;
    }
}
