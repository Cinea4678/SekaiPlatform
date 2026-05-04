using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record SyncJobRequest(
    [property: JsonPropertyName("source")] string? Source);

internal sealed record SyncJobResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("job_type")] string JobType,
    [property: JsonPropertyName("trigger_type")] string TriggerType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("started_at")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("ended_at")] DateTimeOffset? EndedAt,
    [property: JsonPropertyName("error_message")] string? ErrorMessage,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
