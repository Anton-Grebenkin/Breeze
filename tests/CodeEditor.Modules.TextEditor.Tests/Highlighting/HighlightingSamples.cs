namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Samples for every custom highlighting definition, by language id.</summary>
internal static class HighlightingSamples
{
    public static IEnumerable<(string Id, HighlightingSample Sample)> All() =>
        WebSamples.All()
            .Concat(SchemaSamples.All())
            .Concat(ConfigSamples.All())
            .Concat(ScriptSamples.All())
            .Concat(LanguageSamples.All());
}
