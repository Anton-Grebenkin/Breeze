namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Structure of a PowerShell command line for checks (<see cref="CommandTokenizer"/>): commands as segments (split by
/// <c>;</c>, <c>|</c>, <c>&amp;&amp;</c>, parentheses and blocks) of unquoted words, plus flags for constructs that
/// must not run without asking.
/// </summary>
/// <param name="Segments">Words of each command; the first is the command name.</param>
public sealed record CommandShape(IReadOnlyList<IReadOnlyList<string>> Segments)
{
    /// <summary>Redirection to or from a file (except <c>2&gt;&amp;1</c> and <c>&gt;$null</c>).</summary>
    public bool HasRedirection { get; init; }

    public bool HasAssignment { get; init; }

    /// <summary>
    /// Invocation bypassing a command name: <c>&amp;</c>, <c>.</c>, or a method call (<c>.Delete()</c>, <c>[IO.File]::</c>).
    /// </summary>
    public bool HasInvocation { get; init; }

    /// <summary>Backtick escapes or a subexpression in a string hide what the command does.</summary>
    public bool HasEscapes { get; init; }

    /// <summary>The segment words show everything the command does.</summary>
    public bool IsTransparent => !(HasRedirection || HasAssignment || HasInvocation || HasEscapes);
}
