namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// Independent review result (ADR 0012): blocking findings come with evidence and the agent must address them;
/// advisory ones are worth a look. Parsed by line marks: anything outside the format is not a finding.
/// </summary>
public sealed record ReviewResult(IReadOnlyList<string> Blocking, IReadOnlyList<string> Advisory)
{
    public const int MaxFindings = 5;

    private const string BlockingMark = "[blocking]";
    private const string AdvisoryMark = "[advisory]";

    public bool Passed => Blocking.Count == 0;

    public static ReviewResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n').Select(line => line.Trim().TrimStart('-', '*', ' ').Trim()).ToList();
        return new ReviewResult(Findings(lines, BlockingMark), Findings(lines, AdvisoryMark));
    }

    private static List<string> Findings(List<string> lines, string mark) =>
    [
        .. lines.Where(line => line.StartsWith(mark, StringComparison.OrdinalIgnoreCase))
            .Select(line => line[mark.Length..].Trim())
            .Where(line => line.Length > 0)
            .Take(MaxFindings),
    ];
}
