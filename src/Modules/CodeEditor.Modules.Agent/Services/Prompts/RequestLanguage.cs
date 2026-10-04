namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>
/// Request language by script, for the turn reminder. Models keep a named language more reliably than "the request
/// language": with a Russian request and an English prompt, models tend to write notes between calls in English.
/// Cyrillic means Russian (the UI languages are English and Russian, ADR 0011); Latin is never named: many languages
/// use it and code identifiers are always Latin. One pass over the text.
/// </summary>
public static class RequestLanguage
{
    /// <summary>Share of Cyrillic among letters for a request to count as Russian: code identifiers dilute it.</summary>
    public const double CyrillicShare = 0.15;

    public static string? Name(string? request)
    {
        if (string.IsNullOrEmpty(request))
        {
            return null;
        }

        var letters = 0;
        var cyrillic = 0;
        foreach (var character in request)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            letters++;
            if (character is >= (char)0x0400 and <= (char)0x04FF)
            {
                cyrillic++;
            }
        }

        return letters > 0 && (double)cyrillic / letters >= CyrillicShare ? "Russian" : null;
    }

    /// <summary>The reminder phrase; empty if no language is named.</summary>
    /// <param name="thinksAloud">
    /// The model reasons aloud in the reply text, so the reasoning language is named too. Models with native reasoning
    /// get no reasoning language: asked to reason in Russian, they tend to draft the answer with code inside the
    /// reasoning and claim "edits are done" instead of calling a tool.
    /// </param>
    public static string Reminder(string? request, bool thinksAloud = false) =>
        Name(request) is not { } name ? string.Empty
        : thinksAloud ? $"Write your reasoning, notes between tool calls and the answer in {name}."
        : $"Write notes between tool calls and the answer in {name}.";
}
