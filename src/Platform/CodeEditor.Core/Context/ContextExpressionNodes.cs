namespace CodeEditor.Core.Context;

// Expression tree nodes. Internal: only ContextExpression is public.

internal sealed class ConstantExpression(bool value) : ContextExpression
{
    public static readonly ConstantExpression True = new(true);
    public static readonly ConstantExpression False = new(false);

    public override bool Evaluate(IContextKeyLookup context) => value;

    public override string ToString() => value ? "true" : "false";
}

internal sealed class KeyExpression(string key) : ContextExpression
{
    public override bool Evaluate(IContextKeyLookup context) => ContextValues.IsTruthy(context.GetValue(key));

    public override string ToString() => key;
}

internal sealed class NotExpression(ContextExpression operand) : ContextExpression
{
    public override bool Evaluate(IContextKeyLookup context) => !operand.Evaluate(context);

    public override string ToString() => $"!{operand}";
}

internal sealed class AndExpression(ContextExpression left, ContextExpression right) : ContextExpression
{
    public override bool Evaluate(IContextKeyLookup context) => left.Evaluate(context) && right.Evaluate(context);

    public override string ToString() => $"({left} && {right})";
}

internal sealed class OrExpression(ContextExpression left, ContextExpression right) : ContextExpression
{
    public override bool Evaluate(IContextKeyLookup context) => left.Evaluate(context) || right.Evaluate(context);

    public override string ToString() => $"({left} || {right})";
}

internal sealed class EqualsExpression(string key, string literal, bool negate) : ContextExpression
{
    public override bool Evaluate(IContextKeyLookup context) =>
        ContextValues.AreEqual(context.GetValue(key), literal) != negate;

    public override string ToString() => $"{key} {(negate ? "!=" : "==")} '{literal}'";
}

internal static class ContextValues
{
    public static bool IsTruthy(object? value) => value switch
    {
        bool flag => flag,
        string text => text.Length > 0,
        _ => false,
    };

    public static bool AreEqual(object? value, string literal) => value switch
    {
        bool flag => bool.TryParse(literal, out var parsed) && parsed == flag,
        string text => string.Equals(text, literal, StringComparison.Ordinal),
        _ => false,
    };
}
