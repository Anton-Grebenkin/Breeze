using CodeEditor.Core.Text;

namespace CodeEditor.Modules.Agent.ViewModels.Approvals;

/// <summary>A diff line in a card: marker, line number and text. A separator between hunks shows "⋯".</summary>
public sealed record DiffLineViewModel(DiffKind Kind, string Text, int Line, bool IsSeparator = false)
{
    public string Marker => IsSeparator ? "⋯" : Kind switch
    {
        DiffKind.Added => "+",
        DiffKind.Removed => "−",
        _ => " ",
    };

    public string LineText => IsSeparator || Line == 0 ? string.Empty : Line.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
