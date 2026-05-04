using System.Text.Json;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// JSON serialization helpers used by source synchronization metadata.
/// </summary>
internal static class SourceSyncJson
{
    /// <summary>
    /// Serializer options shared by sync metadata payloads.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Serializes a sync metadata object with source-sync naming policy.
    /// </summary>
    /// <param name="value">Metadata value to serialize.</param>
    /// <returns>Serialized JSON text.</returns>
    public static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }
}
