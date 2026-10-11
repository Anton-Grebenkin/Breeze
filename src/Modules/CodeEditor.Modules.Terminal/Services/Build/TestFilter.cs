namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// The <c>run_tests</c> filter as the runner wants it. The model passes a part of a test name, several parts joined by
/// <c>|</c>, or a VSTest expression copied from habit (<c>FullyQualifiedName~A|FullyQualifiedName~B</c>); each part
/// becomes a "name contains" condition. A raw expression with <c>&amp;</c>, <c>!</c> or parentheses goes to VSTest as
/// is: it means more than "contains".
/// </summary>
public static class TestFilter
{
    private static readonly string[] Properties = ["FullyQualifiedName", "DisplayName", "ClassName", "Name"];
    private static readonly char[] Operators = ['~', '='];
    private static readonly char[] ExpressionMarks = ['&', '!', '(', ')'];

    /// <summary>The name parts of the filter, without property prefixes and duplicates.</summary>
    public static IReadOnlyList<string> Parts(string filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return [.. filter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(StripProperty)
            .Where(static part => part.Length > 0)
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Arguments for <c>dotnet test</c>: <c>--filter-method</c> per part (MTP) or one <c>--filter</c> (VSTest).</summary>
    public static IReadOnlyList<string> Arguments(string filter, bool testingPlatform)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (!testingPlatform && filter.IndexOfAny(ExpressionMarks) >= 0)
        {
            return ["--filter", filter];
        }

        var parts = Parts(filter);
        return testingPlatform
            ? [.. parts.SelectMany(static part => new[] { "--filter-method", $"*{part}*" })]
            : ["--filter", string.Join('|', parts.Select(static part => "FullyQualifiedName~" + part))];
    }

    private static string StripProperty(string part)
    {
        foreach (var property in Properties)
        {
            if (part.Length > property.Length && part.StartsWith(property, StringComparison.OrdinalIgnoreCase) && Operators.Contains(part[property.Length]))
            {
                return part[(property.Length + 1)..].Trim();
            }
        }

        return part;
    }
}
