namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Model vendors: display title and order, popular coding vendors first (as in Cursor's model picker), the rest
/// alphabetically below.
/// </summary>
public static class ModelVendors
{
    private static readonly (string Id, string Title)[] Popular =
    [
        ("openai", "OpenAI"),
        ("anthropic", "Anthropic"),
        ("google", "Google"),
        ("deepseek", "DeepSeek"),
        ("x-ai", "xAI"),
        ("xiaomi", "Xiaomi"),
        ("qwen", "Qwen"),
        ("moonshotai", "Moonshot AI"),
        ("z-ai", "Z.ai"),
        ("mistralai", "Mistral AI"),
        ("minimax", "MiniMax"),
        ("meta-llama", "Meta"),
    ];

    public static string VendorOf(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var slash = model.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 ? model[..slash] : string.Empty;
    }

    /// <summary>The model id without the vendor: <c>gpt-6-luna</c>.</summary>
    public static string ShortName(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model[(model.LastIndexOf('/') + 1)..];
    }

    public static string Title(string vendor)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        foreach (var (id, title) in Popular)
        {
            if (string.Equals(id, vendor, StringComparison.OrdinalIgnoreCase))
            {
                return title;
            }
        }

        return vendor.Length == 0 ? vendor : char.ToUpperInvariant(vendor[0]) + vendor[1..];
    }

    public static bool IsPopular(string vendor) => Rank(vendor) < Popular.Length;

    /// <summary>Position in the list: popular vendors in order, all others after them.</summary>
    public static int Rank(string vendor)
    {
        var index = Array.FindIndex(Popular, item => string.Equals(item.Id, vendor, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? Popular.Length : index;
    }
}
