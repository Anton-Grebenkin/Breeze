using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// Approval cards for document edits: a binary file is shown as text (the document's Markdown, a sheet table, a list of
/// cells "before → after"), and the card builds a regular diff from it.
/// </summary>
internal static class ChangePreviews
{
    /// <param name="before">Previous document text; <c>null</c> if the file doesn't exist and is being created.</param>
    public static FileChangePreview Of(DocumentPath target, string? before, string after, string title) => before is null
        ? new FileChangePreview(ProposedChangeKind.Create, target.Relative, string.Empty, after) { Title = title }
        : new FileChangePreview(ProposedChangeKind.Edit, target.Relative, before, after) { Title = title };

    /// <exception cref="AgentToolException">The argument is missing.</exception>
    public static string Required(string? value, string name, string action) => string.IsNullOrEmpty(value)
        ? throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Resources.Strings.ArgumentRequired, action, name))
        : value;

    public static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
