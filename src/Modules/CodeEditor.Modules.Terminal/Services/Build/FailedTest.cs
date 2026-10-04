namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>A failed test: full name and the gist (assertion message and user stack frames, no framework frames).</summary>
public sealed record FailedTest(string Name, IReadOnlyList<string> Details);
