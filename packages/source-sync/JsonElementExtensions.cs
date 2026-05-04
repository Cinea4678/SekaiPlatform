using System.Globalization;
using System.Text.Json;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Provides tolerant readers for loosely typed Moe Sekai JSON payloads.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Reads a string-like object property without throwing when the source JSON is missing or loosely typed.
    /// </summary>
    /// <param name="element">The JSON element expected to contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The string value, the raw numeric text for number values, or <c>null</c> for missing/unsupported values.</returns>
    public static string? GetStringOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    /// <summary>
    /// Reads a string-like object property and falls back when the value is missing or blank.
    /// </summary>
    /// <param name="element">The JSON element expected to contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="fallback">The value to return when the source value is missing or blank.</param>
    /// <returns>The source string value, or <paramref name="fallback"/> when no usable value exists.</returns>
    public static string GetStringOrDefault(this JsonElement element, string propertyName, string fallback)
    {
        var value = element.GetStringOrNull(propertyName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>
    /// Reads an integer object property without throwing when the source JSON is missing or loosely typed.
    /// </summary>
    /// <param name="element">The JSON element expected to contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The integer value for JSON numbers or integer strings, otherwise <c>null</c>.</returns>
    public static int? GetIntOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    /// <summary>
    /// Reads a boolean object property without throwing when the source JSON is missing or not a boolean.
    /// </summary>
    /// <param name="element">The JSON element expected to contain the property.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The boolean value when the property is a JSON boolean, otherwise <c>null</c>.</returns>
    public static bool? GetBoolOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    /// <summary>
    /// Enumerates an array object property without throwing when the property is missing or not an array.
    /// </summary>
    /// <param name="element">The JSON element expected to contain the array property.</param>
    /// <param name="propertyName">The array property name to enumerate.</param>
    /// <returns>The array items, or an empty sequence when the property is missing or not an array.</returns>
    public static IEnumerable<JsonElement> EnumerateArrayProperty(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in property.EnumerateArray())
        {
            yield return item;
        }
    }
}
