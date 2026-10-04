namespace CodeEditor.Core.Context;

/// <summary>
/// Parsed <c>when</c> condition, e.g. <c>editorFocus &amp;&amp; !readOnly</c> or <c>activePanel == 'explorer'</c>.
/// Parsed once at registration, evaluated many times without allocations.
/// </summary>
/// <remarks>
/// Grammar (lowest precedence first):
/// <code>
/// or      := and ('||' and)*
/// and     := unary ('&amp;&amp;' unary)*
/// unary   := '!' unary | primary
/// primary := '(' or ')' | 'true' | 'false' | key (('==' | '!=') value)?
/// value   := word | 'quoted string'
/// </code>
/// A key is truthy when it is <c>true</c> or a non-empty string.
/// </remarks>
public abstract class ContextExpression
{
    private protected ContextExpression()
    {
    }

    /// <exception cref="ContextExpressionException">Syntax error; carries the position.</exception>
    public static ContextExpression Parse(string text) => ContextExpressionParser.Parse(text);

    public abstract bool Evaluate(IContextKeyLookup context);
}
