using System.Text;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Parses a PowerShell command line for checking, not execution (ADR 0012): splits it into commands and words,
/// respecting quotes, and flags constructs that hide what the command does. Conservative: anything not understood is
/// marked opaque, so the command goes to approval. O(n) in the line length.
/// </summary>
public static class CommandTokenizer
{
    public static CommandShape Parse(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var parser = new Parser(command);
        parser.Run();
        return parser.Result();
    }

    private sealed class Parser(string text)
    {
        private readonly List<IReadOnlyList<string>> _segments = [];
        private readonly List<string> _words = [];
        private readonly StringBuilder _word = new();
        private bool _redirection;
        private bool _assignment;
        private bool _invocation;
        private bool _escapes;
        private int _index;

        public CommandShape Result() => new(_segments)
        {
            HasRedirection = _redirection,
            HasAssignment = _assignment,
            HasInvocation = _invocation,
            HasEscapes = _escapes,
        };

        public void Run()
        {
            for (_index = 0; _index < text.Length; _index++)
            {
                Step(text[_index]);
            }

            EndSegment();
        }

        private void Step(char current)
        {
            switch (current)
            {
                case ' ' or '\t' or '\r':
                    EndWord();
                    break;
                case '\n' or ';' or '|' or '{' or '}' or ')':
                    EndSegment();
                    break;
                case '(':
                    OpenParenthesis();
                    break;
                case '&':
                    Ampersand();
                    break;
                case '>' or '<':
                    Redirection(current);
                    break;
                case '\'':
                    SingleQuoted();
                    break;
                case '"':
                    DoubleQuoted();
                    break;
                case '`':
                    _escapes = true;
                    break;
                case '=' when _words.Count == 0 && _word.Length > 0 && _word[0] == '$':
                    _assignment = true;
                    break;
                default:
                    _word.Append(current);
                    break;
            }
        }

        // "(" after ".Name" or "::" is a method call; otherwise a subexpression whose commands form a new segment.
        private void OpenParenthesis()
        {
            var word = _word.ToString();
            var dot = word.LastIndexOf('.');
            if (word.Contains("::", StringComparison.Ordinal)
                || (dot >= 0 && dot < word.Length - 1 && word[(dot + 1)..].All(char.IsLetterOrDigit)))
            {
                _invocation = true;
            }

            EndSegment();
        }

        // "&&" chains commands; a single "&" anywhere is the call operator.
        private void Ampersand()
        {
            if (Next == '&')
            {
                _index++;
                EndSegment();
                return;
            }

            _invocation = true;
            EndWord();
        }

        private void Redirection(char current)
        {
            var rest = text.AsSpan(_index + 1);
            var stream = _word.ToString();
            if (current == '>' && stream is "2" && rest.StartsWith("&1", StringComparison.Ordinal))
            {
                _word.Clear();
                _index += 2;
                return;
            }

            if (current == '>' && stream is "" or "2" or "*" && rest.StartsWith("$null", StringComparison.OrdinalIgnoreCase))
            {
                _word.Clear();
                _index += "$null".Length;
                return;
            }

            _redirection = true;
            EndWord();
        }

        private void SingleQuoted()
        {
            for (_index++; _index < text.Length; _index++)
            {
                if (text[_index] == '\'' && Next == '\'')
                {
                    _word.Append('\'');
                    _index++;
                }
                else if (text[_index] == '\'')
                {
                    return;
                }
                else
                {
                    _word.Append(text[_index]);
                }
            }
        }

        // Inside double quotes PowerShell expands variables and runs "$(…)": a subexpression hides a command.
        private void DoubleQuoted()
        {
            for (_index++; _index < text.Length && text[_index] != '"'; _index++)
            {
                if (text[_index] == '`' || (text[_index] == '$' && Next == '('))
                {
                    _escapes = true;
                }

                _word.Append(text[_index]);
            }
        }

        private char Next => _index + 1 < text.Length ? text[_index + 1] : '\0';

        private void EndWord()
        {
            if (_word.Length == 0)
            {
                return;
            }

            var word = _word.ToString();
            _word.Clear();
            if (_words.Count == 0 && word is "." or "&")
            {
                _invocation = true;
            }

            if (_words.Count == 0 && word.StartsWith('$') && (word.Contains('=') || NextNonSpace() == '='))
            {
                _assignment = true;
            }

            _words.Add(word);
        }

        private char NextNonSpace()
        {
            var position = _index;
            while (position < text.Length && text[position] is ' ' or '\t')
            {
                position++;
            }

            return position < text.Length ? text[position] : '\0';
        }

        private void EndSegment()
        {
            EndWord();
            if (_words.Count > 0)
            {
                _segments.Add([.. _words]);
                _words.Clear();
            }
        }
    }
}
