using System.Globalization;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Context;

/// <summary>
/// Syntax error in a <c>when</c> condition.
/// </summary>
public sealed class ContextExpressionException(string reason, string expression, int position)
    : FormatException(string.Format(CultureInfo.CurrentCulture, Strings.ContextExpressionError, reason, position, expression))
{
    public string Reason { get; } = reason;

    public string Expression { get; } = expression;

    /// <summary>Index of the character where parsing stopped.</summary>
    public int Position { get; } = position;
}
