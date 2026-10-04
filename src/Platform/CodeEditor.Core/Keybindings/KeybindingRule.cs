using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Context;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Keybindings;

/// <summary>
/// One <c>keybindings.json</c> rule. An invalid rule carries a user-facing error instead of throwing,
/// so one typo does not disable the other bindings.
/// </summary>
internal sealed record KeybindingRule(KeySequence? Sequence, string CommandId, bool IsRemoval, ContextExpression? When, object? Argument, string? Error)
{
    private const string RemovalPrefix = "-";

    public static KeybindingRule Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid(Strings.KeybindingRuleNotObject);
        }

        var command = GetString(element, "command");
        if (string.IsNullOrWhiteSpace(command) || command == RemovalPrefix)
        {
            return Invalid(Strings.KeybindingRuleNoCommand);
        }

        var isRemoval = command.StartsWith(RemovalPrefix, StringComparison.Ordinal);
        var commandId = isRemoval ? command[RemovalPrefix.Length..] : command;
        var key = GetString(element, "key");

        KeySequence? sequence = null;
        if (key is not null)
        {
            if (!KeyGestureParser.TryParse(key, out var parsed))
            {
                return Invalid(Strings.KeybindingRuleUnknownKey, key, command);
            }

            sequence = parsed;
        }
        else if (!isRemoval)
        {
            return Invalid(Strings.KeybindingRuleNoKey, command);
        }

        ContextExpression? when = null;
        if (GetString(element, "when") is { } whenText)
        {
            try
            {
                when = ContextExpression.Parse(whenText);
            }
            catch (ContextExpressionException exception)
            {
                return Invalid(Strings.KeybindingRuleInvalidWhen, whenText, command, exception.Message);
            }
        }

        return new KeybindingRule(sequence, commandId, isRemoval, when, ReadArgument(element), null);
    }

    private static KeybindingRule Invalid(string error) => new(null, string.Empty, false, null, null, error);

    private static KeybindingRule Invalid(string template, params object?[] args) =>
        Invalid(string.Format(CultureInfo.CurrentCulture, template, args));

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Command argument: string, int, double or bool; complex values are passed as raw JSON text.</summary>
    private static object? ReadArgument(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return null;
        }

        return args.ValueKind switch
        {
            JsonValueKind.String => args.GetString(),
            JsonValueKind.Number when args.TryGetInt32(out var number) => number,
            JsonValueKind.Number => args.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => args.GetRawText(),
        };
    }
}
