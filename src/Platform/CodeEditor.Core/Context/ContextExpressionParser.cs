using System.Buffers;
using System.Globalization;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Context;

/// <summary>
/// Single-pass recursive descent parser for <c>when</c> conditions, O(n). Grammar: see <see cref="ContextExpression"/>.
/// </summary>
internal sealed class ContextExpressionParser
{
    private const char Quote = '\'';

    // Characters not allowed in a key or an unquoted value.
    private static readonly SearchValues<char> SpecialChars = SearchValues.Create("()!=&|'");

    private readonly string _text;
    private int _position;

    private ContextExpressionParser(string text) => _text = text;

    private bool AtEnd => _position >= _text.Length;

    public static ContextExpression Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parser = new ContextExpressionParser(text);
        var expression = parser.ParseOr();

        parser.SkipWhitespace();
        return parser.AtEnd ? expression : throw parser.Error(Strings.ContextExpressionExtraText);
    }

    private ContextExpression ParseOr()
    {
        var left = ParseAnd();
        while (TryConsume("||"))
        {
            left = new OrExpression(left, ParseAnd());
        }

        return left;
    }

    private ContextExpression ParseAnd()
    {
        var left = ParseUnary();
        while (TryConsume("&&"))
        {
            left = new AndExpression(left, ParseUnary());
        }

        return left;
    }

    private ContextExpression ParseUnary() =>
        TryConsume("!") ? new NotExpression(ParseUnary()) : ParsePrimary();

    private ContextExpression ParsePrimary()
    {
        SkipWhitespace();
        if (AtEnd)
        {
            throw Error(Strings.ContextExpressionConditionExpected);
        }

        if (TryConsume("("))
        {
            var inner = ParseOr();
            return TryConsume(")") ? inner : throw Error(Strings.ContextExpressionClosingParenExpected);
        }

        var key = ReadWord();
        if (key.Length == 0)
        {
            throw Error(string.Format(CultureInfo.CurrentCulture, Strings.ContextExpressionUnexpectedChar, _text[_position]));
        }

        if (TryConsume("=="))
        {
            return new EqualsExpression(key, ReadValue(), negate: false);
        }

        if (TryConsume("!="))
        {
            return new EqualsExpression(key, ReadValue(), negate: true);
        }

        return key switch
        {
            "true" => ConstantExpression.True,
            "false" => ConstantExpression.False,
            _ => new KeyExpression(key),
        };
    }

    private string ReadValue()
    {
        SkipWhitespace();
        if (AtEnd || _text[_position] != Quote)
        {
            var word = ReadWord();
            return word.Length > 0 ? word : throw Error(Strings.ContextExpressionValueExpected);
        }

        var start = _position + 1;
        var end = _text.IndexOf(Quote, start);
        if (end < 0)
        {
            throw Error(Strings.ContextExpressionUnclosedQuote);
        }

        _position = end + 1;
        return _text[start..end];
    }

    private string ReadWord()
    {
        var start = _position;
        while (!AtEnd && IsWordChar(_text[_position]))
        {
            _position++;
        }

        return _text[start.._position];
    }

    /// <summary>Skips whitespace and consumes the token if it comes next.</summary>
    private bool TryConsume(string token)
    {
        SkipWhitespace();
        if (!_text.AsSpan(_position).StartsWith(token, StringComparison.Ordinal))
        {
            return false;
        }

        // "!" followed by "=" is the "!=" operator, not a negation.
        if (token == "!" && _position + 1 < _text.Length && _text[_position + 1] == '=')
        {
            return false;
        }

        _position += token.Length;
        return true;
    }

    private void SkipWhitespace()
    {
        while (!AtEnd && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }

    private static bool IsWordChar(char c) => !char.IsWhiteSpace(c) && !SpecialChars.Contains(c);

    private ContextExpressionException Error(string reason) => new(reason, _text, _position);
}
