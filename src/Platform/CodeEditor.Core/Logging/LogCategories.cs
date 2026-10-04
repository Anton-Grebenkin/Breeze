namespace CodeEditor.Core.Logging;

/// <summary>
/// Log category without the namespace: <c>CodeEditor.Core.Commands.CommandService</c> becomes <c>CommandService</c>.
/// </summary>
public static class LogCategories
{
    public static string Short(string category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var lastDot = category.LastIndexOf('.');
        return lastDot < 0 ? category : category[(lastDot + 1)..];
    }
}
