using System.Globalization;
using System.Text;
using System.Text.Json;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Settings;

/// <summary>
/// The <c>settings.json</c> format as in VS Code: JSON with comments and trailing commas, dotted keys
/// (<c>"editor.fontSize": 14</c>) or nested objects. Parses into a flat configuration dictionary and writes single
/// values while keeping comments and formatting.
/// </summary>
public static class SettingsJson
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Configuration keys: <c>"editor.fontSize"</c> → <c>editor:fontSize</c>, arrays <c>key:0</c>.</summary>
    /// <exception cref="JsonException">The text is not a JSON object.</exception>
    public static Dictionary<string, string?> Parse(string json)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return data;
        }

        using var document = JsonDocument.Parse(json, DocumentOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(Strings.SettingsNotObjectExample);
        }

        Flatten(document.RootElement, prefix: null, data);
        return data;
    }

    /// <summary>
    /// Sets a top-level key (<c>"workbench.colorTheme"</c>) without touching the rest of the text: replaces the
    /// existing value or appends the property at the end of the object.
    /// </summary>
    /// <param name="value">A JSON-serializable value: string, number or bool.</param>
    /// <exception cref="JsonException">The file text is corrupt and cannot be written.</exception>
    public static string SetValue(string json, string key, object value)
    {
        var valueJson = JsonSerializer.Serialize(value, value.GetType(), SettingsJsonContext.Readable);
        if (string.IsNullOrWhiteSpace(json))
        {
            return $"{{\n    {Quote(key)}: {valueJson}\n}}\n";
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        var (valueStart, valueEnd, lastValueEnd, objectEnd) = Locate(bytes, key);
        return valueStart >= 0
            ? Splice(bytes, valueStart, valueEnd, valueJson)
            : lastValueEnd >= 0
                ? Splice(bytes, lastValueEnd, lastValueEnd, $",\n    {Quote(key)}: {valueJson}")
                : Splice(bytes, objectEnd, objectEnd, $"\n    {Quote(key)}: {valueJson}\n");
    }

    /// <summary>
    /// Removes a top-level key with its separating comma without touching the rest of the text; returns the text
    /// unchanged when the key is absent.
    /// </summary>
    /// <exception cref="JsonException">The file text is corrupt and cannot be written.</exception>
    public static string RemoveValue(string json, string key)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(Strings.SettingsNotObject);
        }

        var previousValueEnd = -1;
        while (reader.Read() && !(reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0))
        {
            var keyStart = (int)reader.TokenStartIndex;
            var isKey = reader.ValueTextEquals(key);
            reader.Read();
            reader.Skip();
            var valueEnd = (int)reader.BytesConsumed;
            if (!isKey)
            {
                previousValueEnd = valueEnd;
                continue;
            }

            // Not last: cut up to the next key (with the comma). Last: cut from the end of the previous value.
            reader.Read();
            return reader.TokenType != JsonTokenType.EndObject
                ? Splice(bytes, keyStart, (int)reader.TokenStartIndex, string.Empty)
                : Splice(bytes, previousValueEnd >= 0 ? previousValueEnd : keyStart, valueEnd, string.Empty);
        }

        return reader.TokenType == JsonTokenType.EndObject
            ? json
            : throw new JsonException(Strings.SettingsNoClosingBrace);
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, string?> data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    // A dot in a top-level key separates sections; in nested keys it is part of the name
                    // (the "**/*.tmp" pattern in files.exclude).
                    var name = prefix is null ? property.Name.Replace('.', ':') : property.Name;
                    Flatten(property.Value, prefix is null ? name : $"{prefix}:{name}", data);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, $"{prefix}:{index++.ToString(CultureInfo.InvariantCulture)}", data);
                }

                break;
            case JsonValueKind.Null:
                data[prefix!] = null;
                break;
            case JsonValueKind.String:
                data[prefix!] = element.GetString();
                break;
            default:
                data[prefix!] = element.GetRawText();
                break;
        }
    }

    /// <summary>Bounds of the key's value, end of the last value and position of the root's closing brace.</summary>
    private static (int ValueStart, int ValueEnd, int LastValueEnd, int ObjectEnd) Locate(byte[] bytes, string key)
    {
        var reader = new Utf8JsonReader(bytes, ReaderOptions);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(Strings.SettingsNotObject);
        }

        int valueStart = -1, valueEnd = -1, lastValueEnd = -1;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
            {
                return (valueStart, valueEnd, lastValueEnd, (int)reader.TokenStartIndex);
            }

            var isKey = reader.ValueTextEquals(key);
            reader.Read();
            var start = (int)reader.TokenStartIndex;
            reader.Skip();
            var end = (int)reader.BytesConsumed;
            if (isKey)
            {
                (valueStart, valueEnd) = (start, end);
            }

            lastValueEnd = end;
        }

        throw new JsonException(Strings.SettingsNoClosingBrace);
    }

    private static string Splice(byte[] bytes, int start, int end, string insert) =>
        string.Concat(Encoding.UTF8.GetString(bytes, 0, start), insert, Encoding.UTF8.GetString(bytes, end, bytes.Length - end));

    private static string Quote(string key) => JsonSerializer.Serialize(key, SettingsJsonContext.Readable.String);
}
