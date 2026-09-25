using System.Globalization;
using System.Text.Json;

namespace AzureCreditsApp.Services;

/// <summary>
/// Null-safe accessors for nested properties in ARM JSON responses.
/// </summary>
internal static class JsonValues
{
    public static JsonElement? Get(JsonElement element, params string[] path)
    {
        JsonElement current = element;
        foreach (string name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : current;
    }

    public static string? GetString(JsonElement element, params string[] path)
    {
        return Get(element, path) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    }

    public static decimal GetDecimal(JsonElement element, params string[] path)
    {
        return Get(element, path) is JsonElement value ? ToDecimal(value) : 0m;
    }

    public static DateTimeOffset? GetDate(JsonElement element, params string[] path)
    {
        return GetString(element, path) is string text
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date)
            ? date
            : null;
    }

    public static decimal ToDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetDecimal(out decimal number) ? number : (decimal)value.GetDouble();
        }

        return value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : 0m;
    }
}
