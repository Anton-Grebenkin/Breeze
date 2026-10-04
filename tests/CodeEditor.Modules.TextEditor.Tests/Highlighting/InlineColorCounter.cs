using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>
/// Counts colors set inline in a rule (<c>foreground="Red"</c>) instead of referencing a named color: those aren't
/// recolored by the theme. The loader also gives colorless rules an empty color; that doesn't count.
/// </summary>
internal sealed class InlineColorCounter : IXshdVisitor
{
    private int _count;

    public static int Count(XshdSyntaxDefinition syntax)
    {
        var counter = new InlineColorCounter();
        syntax.AcceptElements(counter);
        return counter._count;
    }

    public object? VisitRuleSet(XshdRuleSet ruleSet)
    {
        ruleSet.AcceptElements(this);
        return null;
    }

    public object? VisitColor(XshdColor color) => null;

    public object? VisitKeywords(XshdKeywords keywords)
    {
        Check(keywords.ColorReference);
        return null;
    }

    public object? VisitSpan(XshdSpan span)
    {
        Check(span.SpanColorReference);
        Check(span.BeginColorReference);
        Check(span.EndColorReference);
        span.RuleSetReference.InlineElement?.AcceptVisitor(this);
        return null;
    }

    public object? VisitImport(XshdImport import) => null;

    public object? VisitRule(XshdRule rule)
    {
        Check(rule.ColorReference);
        return null;
    }

    private void Check(XshdReference<XshdColor> reference)
    {
        if (reference.InlineElement is { } color && IsSet(color))
        {
            _count++;
        }
    }

    private static bool IsSet(XshdColor color) =>
        color.Foreground is not null || color.Background is not null || color.FontWeight is not null || color.FontStyle is not null
        || color.FontFamily is not null || color.FontSize is not null || color.Underline is not null || color.Strikethrough is not null;
}
