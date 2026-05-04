using System.Text.Json;

namespace SekaiPlatform.SourceSync;

internal static class SourceSyncJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }
}
